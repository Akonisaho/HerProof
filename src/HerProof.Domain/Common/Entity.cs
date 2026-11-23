namespace HerProof.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

/// <summary>Marker for entities the application layer must never UPDATE or DELETE. Enforced by
/// HerProof.Infrastructure's AppendOnlyInterceptor and by DB privileges in production.</summary>
public interface IAppendOnly
{
}
