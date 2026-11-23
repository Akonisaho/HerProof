using HerProof.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HerProof.Infrastructure.Persistence;

/// <summary>Enforces docs/02 rule 1 and CLAUDE.md rule 8: verification_records, assessment_reviews,
/// consent_records, audit_log, integrity_events and interview_turns are append-only. No application
/// code path — including a bug — can UPDATE or DELETE a row of these types once SaveChanges runs.</summary>
public class AppendOnlyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var violations = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IAppendOnly && e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.GetType().Name)
            .ToList();

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"Append-only violation: attempted to update or delete {string.Join(", ", violations)}. " +
                "These entities may only be inserted (docs/02 rule 1, CLAUDE.md rule 8).");
        }
    }
}
