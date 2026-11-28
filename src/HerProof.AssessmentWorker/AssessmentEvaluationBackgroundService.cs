using System.Text.Json;
using HerProof.Application.Assessments;
using HerProof.Application.Repositories;
using HerProof.Application.Sandbox;
using HerProof.Domain.Assessments;
using HerProof.Domain.Competencies;
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
            .Include(a => a.Questions)
            .FirstAsync(a => a.Id == session.AssessmentId, cancellationToken);

        logger.LogInformation("Claimed submission {SubmissionId} for assessment {AssessmentId}", submission.Id, assessment.Id);

        try
        {
            var submittedFiles = JsonSerializer.Deserialize<List<SubmittedFile>>(submission.SubmissionReference) ?? [];
            var questionTests = assessment.Questions
                .Select(q => (q.FileName, q.HiddenTestCode))
                .ToList();
            var sandboxLanguage = assessment.Language == AssessmentLanguage.Java ? SandboxLanguage.Java : SandboxLanguage.CSharp;
            var files = AssessmentSandboxProjectScaffold.Build(submittedFiles, questionTests, sandboxLanguage);
            var content = new FetchedRepositoryContent("submission", files.Select(f => new FetchedRepositoryFile(f.Path, f.Content)).ToList());

            var sandboxRunner = scope.ServiceProvider.GetRequiredService<ISandboxRunner>();
            var sandboxResult = await SandboxWorkspace.WithMaterializedContentAsync(content, path => sandboxRunner.RunAsync(path, sandboxLanguage, cancellationToken));

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
            await EnsureEvidenceSubmittedAsync(db, session.CandidateId, assessment.Id, submission.Id, cancellationToken);

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

    /// <summary>The same real gap InterviewController.EnsureEvidenceSubmittedAsync closed for
    /// interviews: until now, a candidate could submit and pass a practical assessment and it would
    /// never reach the Assessor inbox at all, because nothing here ever touched CandidateCompetency -
    /// the inbox (AssessorDashboardController.Index) only lists EvidenceSubmitted/UnderAssessment/
    /// AssessmentCompleted rows. One row per competency this assessment actually covers (via
    /// AssessmentCompetencies), evidenced by this submission's id.</summary>
    private static async Task EnsureEvidenceSubmittedAsync(HerProofDbContext db, Guid candidateId, Guid assessmentId, Guid submissionId, CancellationToken cancellationToken)
    {
        var competencyIds = await db.AssessmentCompetencies.AsNoTracking()
            .Where(ac => ac.AssessmentId == assessmentId)
            .Select(ac => ac.CompetencyId)
            .ToListAsync(cancellationToken);
        if (competencyIds.Count == 0)
        {
            return;
        }

        var existing = await db.CandidateCompetencies
            .Where(cc => cc.CandidateId == candidateId && competencyIds.Contains(cc.CompetencyId))
            .ToDictionaryAsync(cc => cc.CompetencyId, cancellationToken);

        foreach (var competencyId in competencyIds)
        {
            if (!existing.TryGetValue(competencyId, out var candidateCompetency))
            {
                candidateCompetency = new CandidateCompetency(candidateId, competencyId, declaredLevel: null, DateTime.UtcNow);
                db.CandidateCompetencies.Add(candidateCompetency);
            }

            // db.Add here, not relying on the collection navigation - the same EF mistracking gotcha
            // InterviewController.EnsureEvidenceSubmittedAsync already documents and works around.
            var evidence = candidateCompetency.AddEvidence("PracticalAssessment", submissionId.ToString(), $"Practical assessment submitted {DateTime.UtcNow:u}.", DateTime.UtcNow);
            db.Add(evidence);
        }
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
