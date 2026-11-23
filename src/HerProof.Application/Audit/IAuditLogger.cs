namespace HerProof.Application.Audit;

/// <summary>Writes to the append-only audit_log (docs/02 rule 1). CLAUDE.md P03: sign-in, role
/// changes and permission failures must all be audited, with no secrets in the detail payload.</summary>
public interface IAuditLogger
{
    Task LogAsync(Guid? actorUserId, string action, string entityType, Guid entityId, string detailJson, CancellationToken cancellationToken = default);
}
