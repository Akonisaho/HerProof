using System.Text.Json;
using HerProof.AI;
using HerProof.Application.Repositories;
using HerProof.Application.Sandbox;
using HerProof.Domain.Repositories;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HerProof.AssessmentWorker;

/// <summary>ADR-02: Postgres as the job queue, `FOR UPDATE SKIP LOCKED` so multiple worker instances
/// never double-claim a job. Picks up `Pending` RepositoryAnalysis rows that RepositoriesController
/// enqueues, runs the full P08 pipeline, and persists findings — resolving HP-014 (analysis no longer
/// runs on the web request thread).</summary>
public class RepositoryAnalysisBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RepositoryAnalysisBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await TryProcessOneAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in repository analysis worker loop.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> TryProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();

        var analysisId = await ClaimNextPendingAnalysisAsync(db, cancellationToken);
        if (analysisId is null)
        {
            return false;
        }

        var analysis = await db.RepositoryAnalyses.FirstAsync(a => a.Id == analysisId, cancellationToken);
        var repository = await db.Repositories.AsNoTracking().FirstAsync(r => r.Id == analysis.RepositoryId, cancellationToken);

        logger.LogInformation("Claimed analysis {AnalysisId} for repository {RepositoryUrl}", analysis.Id, repository.RepositoryUrl);

        try
        {
            var parsed = GitHubRepositoryUrlParser.TryParse(repository.RepositoryUrl)
                ?? throw new InvalidOperationException($"Stored repository URL is not parseable: {repository.RepositoryUrl}");

            var fetcher = scope.ServiceProvider.GetRequiredService<RepositoryContentFetcher>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<RepositoryAnalysisOrchestrator>();

            var content = await fetcher.FetchAsync(parsed.Owner, parsed.Repo, repository.CommitSha, cancellationToken);
            var result = await orchestrator.AnalyzeAsync(content, cancellationToken);

            var competencyIds = await db.Competencies.AsNoTracking()
                .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (var finding in result.StaticFindings.Concat(result.VerifiedLlmFindings.Select(f => new StaticFinding(f.CompetencyName, f.FindingType, f.FilePath, f.LineReference, f.Description, f.Evidence))))
            {
                if (competencyIds.TryGetValue(finding.CompetencyName, out var competencyId))
                {
                    var persisted = analysis.AddFinding(competencyId, finding.FindingType, finding.FilePath, finding.LineReference ?? "", finding.Description, finding.Evidence, "High");
                    db.Add(persisted);
                }
            }

            var sandboxRunner = scope.ServiceProvider.GetRequiredService<ISandboxRunner>();
            var sandboxResult = await SandboxWorkspace.WithMaterializedContentAsync(content, path => sandboxRunner.RunAsync(path, cancellationToken: cancellationToken));
            // STUB(HP-017): the sandbox entrypoint reports only a pass/fail boolean, not per-test
            // counts (that would need parsing the .trx inside the container). 1/0 here is a stand-in
            // signal, not a real count — docs/02's schema only models counts, not a tri-state "ran".
            var executionReport = analysis.RecordExecution(
                sandboxResult.RestoreOk, sandboxResult.BuildOk, sandboxResult.TestsOk ? 1 : 0, sandboxResult.TestsOk ? 0 : 1,
                sandboxResult.LogExcerpt, sandboxResult.SandboxImageDigest);
            db.Add(executionReport);

            analysis.Complete(JsonSerializer.Serialize(result.TechnologiesDetected), result.Summary, result.ModelName, result.PromptVersion, DateTime.UtcNow);
            logger.LogInformation("Completed analysis {AnalysisId}: {StaticCount} static + {LlmCount} AI finding(s)",
                analysis.Id, result.StaticFindings.Count, result.VerifiedLlmFindings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis {AnalysisId} failed.", analysis.Id);
            analysis.Fail();
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Raw SQL because EF Core has no first-class `FOR UPDATE SKIP LOCKED` API. Claims and
    /// marks InProgress atomically in one statement, so two worker instances can never race on the
    /// same row (ADR-02).</summary>
    private static async Task<Guid?> ClaimNextPendingAnalysisAsync(HerProofDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE repository_analyses
            SET status = 'InProgress'
            WHERE id = (
                SELECT id FROM repository_analyses
                WHERE status = 'Pending'
                ORDER BY created_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING id;
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }
}
