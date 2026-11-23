using System.Text.Json;
using HerProof.Application.Assessments;
using HerProof.Application.Repositories;
using HerProof.Application.Sandbox;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HerProof.AssessmentWorker;

/// <summary>ADR-02: claims assessment_submissions the same way RepositoryAnalysisBackgroundService
/// claims repository analyses — `FOR UPDATE SKIP LOCKED`, atomic with marking it claimed, so two
/// worker instances can never double-evaluate the same submission. Produces a recommendation only
/// (docs/05 P13): never creates a VerificationRecord, never sets a session to Reviewed.</summary>
public class AssessmentEvaluationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AssessmentEvaluationBackgroundService> logger) : BackgroundService
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
                logger.LogError(ex, "Unexpected error in assessment evaluation worker loop.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> TryProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();

        var submissionId = await ClaimNextUnevaluatedSubmissionAsync(db, cancellationToken);
        if (submissionId is null)
        {
            return false;
        }

        var submission = await db.AssessmentSubmissions.FirstAsync(s => s.Id == submissionId, cancellationToken);
        var session = await db.AssessmentSessions.FirstAsync(s => s.Id == submission.SessionId, cancellationToken);
        var assessment = await db.Assessments
            .Include(a => a.RubricCriteria)
            .FirstAsync(a => a.Id == session.AssessmentId, cancellationToken);

        logger.LogInformation("Claimed submission {SubmissionId} for assessment {AssessmentId}", submission.Id, assessment.Id);

        try
        {
            var files = JsonSerializer.Deserialize<List<SubmittedFile>>(submission.SubmissionReference) ?? [];
            var content = new FetchedRepositoryContent("submission", files.Select(f => new FetchedRepositoryFile(f.Path, f.Content)).ToList());

            var sandboxRunner = scope.ServiceProvider.GetRequiredService<ISandboxRunner>();
            var sandboxResult = await SandboxWorkspace.WithMaterializedContentAsync(content, path => sandboxRunner.RunAsync(path, cancellationToken));

            var criteria = assessment.RubricCriteria.Select(c => (c.Id, c.Name, c.Weight)).ToList();
            var scoring = AssessmentScoring.Score(criteria, sandboxResult.RestoreOk, sandboxResult.BuildOk, sandboxResult.TestsOk, sandboxResult.Executed);

            var resultJson = JsonSerializer.Serialize(new
            {
                overallScore = scoring.OverallScore,
                criterionScores = scoring.CriterionScores,
                sandbox = new { sandboxResult.Executed, sandboxResult.RestoreOk, sandboxResult.BuildOk, sandboxResult.TestsOk, sandboxResult.LogExcerpt },
            });

            submission.RecordAutomatedResults(resultJson);
            session.MarkEvaluated();

            logger.LogInformation("Evaluated submission {SubmissionId}: overall score {Score}", submission.Id, scoring.OverallScore);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Evaluation of submission {SubmissionId} failed; leaving it claimed rather than fabricating a result.", submission.Id);
            // STUB(HP-029): no retry path for a worker-side failure after claiming — the submission
            // stays marked "claiming" forever. Acceptable for a hackathon's traffic; needs a real
            // dead-letter/retry story before production.
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<Guid?> ClaimNextUnevaluatedSubmissionAsync(HerProofDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE assessment_submissions
            SET test_results_json = '"claiming"'
            WHERE id = (
                SELECT id FROM assessment_submissions
                WHERE test_results_json IS NULL
                ORDER BY submitted_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING id;
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid id ? id : null;
    }
}
