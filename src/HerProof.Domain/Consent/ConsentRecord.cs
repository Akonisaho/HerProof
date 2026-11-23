using HerProof.Domain.Common;

namespace HerProof.Domain.Consent;

/// <summary>Append-only (docs/02 rule 1). One row per consent event, never updated.</summary>
public class ConsentRecord : Entity, IAppendOnly
{
    public Guid UserId { get; init; }
    public string Purpose { get; init; }
    public string Version { get; init; }
    public bool Granted { get; init; }
    public DateTime RecordedAtUtc { get; init; }

    private ConsentRecord()
    {
        Purpose = string.Empty;
        Version = string.Empty;
    }

    public ConsentRecord(Guid userId, string purpose, string version, bool granted, DateTime recordedAtUtc)
    {
        UserId = userId;
        Purpose = purpose;
        Version = version;
        Granted = granted;
        RecordedAtUtc = recordedAtUtc;
    }
}
