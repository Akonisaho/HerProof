using HerProof.Domain.Common;

namespace HerProof.Domain.Audit;

/// <summary>Append-only (docs/02 rule 1). Written for every evidence and verification use case,
/// and for sign-in, role changes and permission failures (CLAUDE.md P03).</summary>
public class AuditLogEntry : Entity, IAppendOnly
{
    public Guid ActorUserId { get; init; }
    public string Action { get; init; }
    public string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public string DetailJson { get; init; }
    public DateTime AtUtc { get; init; }

    private AuditLogEntry()
    {
        Action = string.Empty;
        EntityType = string.Empty;
        DetailJson = "{}";
    }

    public AuditLogEntry(Guid actorUserId, string action, string entityType, Guid entityId, string detailJson, DateTime atUtc)
    {
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        DetailJson = detailJson;
        AtUtc = atUtc;
    }
}
