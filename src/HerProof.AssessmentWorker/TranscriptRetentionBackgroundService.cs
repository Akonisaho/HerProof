using HerProof.Application.Audit;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HerProof.AssessmentWorker;

/// <summary>docs/05 P17 retention job. Runs daily and redacts interview_turns.transcript_confirmed/
/// transcript_raw older than RetentionPolicy.TranscriptRetentionWindow. Uses raw SQL deliberately:
/// InterviewTurn is IAppendOnly, so AppendOnlyInterceptor blocks an EF-tracked UPDATE/DELETE through
/// SaveChanges by design (CLAUDE.md rule 8) — the same way the job-claiming queries elsewhere in this
/// project already use raw SQL for their own reasons. This is the one sanctioned, logged exception,
/// not a way around the guard: it only ever replaces old transcript text with a fixed placeholder,
/// never edits a verification, review or audit record.</summary>
public class TranscriptRetentionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TranscriptRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in transcript retention job.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

        var cutoff = DateTime.UtcNow - RetentionPolicy.TranscriptRetentionWindow;

        var redacted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE interview_turns
             SET transcript_confirmed = {RetentionPolicy.RedactedPlaceholder}, transcript_raw = NULL
             WHERE created_at_utc < {cutoff}
               AND (transcript_confirmed IS DISTINCT FROM {RetentionPolicy.RedactedPlaceholder}
                    OR transcript_raw IS NOT NULL)
             """,
            cancellationToken);

        if (redacted > 0)
        {
            logger.LogInformation("Transcript retention redacted {Count} interview turn(s) older than {Cutoff}.", redacted, cutoff);
            await auditLogger.LogAsync(
                actorUserId: null,
                action: "TranscriptRetentionRedaction",
                entityType: "InterviewTurn",
                entityId: Guid.Empty,
                detailJson: $$"""{"redactedCount": {{redacted}}, "cutoffUtc": "{{cutoff:O}}"}""",
                cancellationToken);
        }
    }
}
