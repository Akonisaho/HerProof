using HerProof.Application.Audit;
using HerProof.Domain.Audit;
using HerProof.Infrastructure.Persistence;

namespace HerProof.Infrastructure.Audit;

public class EfAuditLogger(HerProofDbContext db) : IAuditLogger
{
    /// <summary>No authenticated actor (e.g. a failed login for an unknown email). Distinct from a
    /// real user id so audit queries can tell "no one" apart from "a specific user".</summary>
    public static readonly Guid SystemActorId = Guid.Empty;

    public async Task LogAsync(Guid? actorUserId, string action, string entityType, Guid entityId, string detailJson, CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry(actorUserId ?? SystemActorId, action, entityType, entityId, detailJson, DateTime.UtcNow);
        db.AuditLog.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
