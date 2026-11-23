using HerProof.Domain.Common;

namespace HerProof.Domain.Passports;

public enum PassportStatus
{
    Active,
    Suspended,
}

public class SkillsPassport : Entity
{
    private readonly List<PassportCredential> _credentials = [];
    private readonly List<PassportShare> _shares = [];

    public Guid CandidateId { get; init; }
    public string PassportNumber { get; init; }
    public PassportStatus Status { get; private set; } = PassportStatus.Active;
    public DateTime CreatedAtUtc { get; init; }

    public IReadOnlyCollection<PassportCredential> Credentials => _credentials;
    public IReadOnlyCollection<PassportShare> Shares => _shares;

    private SkillsPassport()
    {
        PassportNumber = string.Empty;
    }

    public SkillsPassport(Guid candidateId, string passportNumber, DateTime createdAtUtc)
    {
        CandidateId = candidateId;
        PassportNumber = passportNumber;
        CreatedAtUtc = createdAtUtc;
    }

    public PassportCredential IssueCredential(Guid verificationRecordId, string credentialIdentifier, DateTime issuedAtUtc, DateTime? validUntilUtc)
    {
        var credential = new PassportCredential(Id, verificationRecordId, credentialIdentifier, issuedAtUtc, validUntilUtc);
        _credentials.Add(credential);
        return credential;
    }

    /// <summary>Field-level visibility allow-list. The public verify page must never expose
    /// recordings, transcripts, private repos, contact details, integrity events or scores
    /// (docs/05 P15) — only fields named here may ever be shown.</summary>
    public PassportShare CreateShare(string tokenHash, IReadOnlyCollection<string> visibleFields, DateTime createdAtUtc, DateTime? expiresAtUtc = null)
    {
        var share = new PassportShare(Id, tokenHash, visibleFields, createdAtUtc, expiresAtUtc);
        _shares.Add(share);
        return share;
    }

    public void Suspend() => Status = PassportStatus.Suspended;
}

public enum CredentialStatus
{
    Active,
    Expired,
    Revoked,
}

public class PassportCredential : Entity
{
    public Guid PassportId { get; init; }
    public Guid VerificationRecordId { get; init; }
    public string CredentialIdentifier { get; init; }
    public DateTime IssuedAtUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public CredentialStatus CredentialStatus { get; private set; } = CredentialStatus.Active;

    private PassportCredential()
    {
        CredentialIdentifier = string.Empty;
    }

    public PassportCredential(Guid passportId, Guid verificationRecordId, string credentialIdentifier, DateTime issuedAtUtc, DateTime? validUntilUtc)
    {
        PassportId = passportId;
        VerificationRecordId = verificationRecordId;
        CredentialIdentifier = credentialIdentifier;
        IssuedAtUtc = issuedAtUtc;
        ValidUntilUtc = validUntilUtc;
    }

    public void Revoke() => CredentialStatus = CredentialStatus.Revoked;

    public void Expire() => CredentialStatus = CredentialStatus.Expired;
}

public class PassportShare : Entity
{
    public Guid PassportId { get; init; }

    /// <summary>A hash of the share token; the raw token appears only in the URL given to the
    /// candidate, never stored (docs/02).</summary>
    public string TokenHash { get; init; }

    public IReadOnlyCollection<string> VisibleFields { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; private set; }

    private PassportShare()
    {
        TokenHash = string.Empty;
        VisibleFields = [];
    }

    public PassportShare(Guid passportId, string tokenHash, IReadOnlyCollection<string> visibleFields, DateTime createdAtUtc, DateTime? expiresAtUtc = null)
    {
        PassportId = passportId;
        TokenHash = tokenHash;
        VisibleFields = visibleFields;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc is not null && nowUtc >= ExpiresAtUtc;

    public void Revoke(DateTime nowUtc) => RevokedAtUtc = nowUtc;
}
