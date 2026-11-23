using HerProof.Domain.Passports;

namespace HerProof.Application.Passports;

/// <summary>docs/05 P15: a share is scoped to specific credentials the candidate picked, never to a
/// whole passport and never to any field outside credential identity (name, level, dates) — recordings,
/// transcripts, private repos, contact details, integrity events and scores are never on this list
/// because they never pass through this type at all (CLAUDE.md rule 9).</summary>
public static class PassportShareService
{
    public static (PassportShare Share, string RawToken) CreateShare(
        SkillsPassport passport, IReadOnlyCollection<string> selectedCredentialIdentifiers, DateTime createdAtUtc, DateTime? expiresAtUtc = null)
    {
        var validIdentifiers = passport.Credentials
            .Select(c => c.CredentialIdentifier)
            .ToHashSet();
        var visibleFields = selectedCredentialIdentifiers
            .Where(validIdentifiers.Contains)
            .Distinct()
            .ToList();

        if (visibleFields.Count == 0)
        {
            throw new ArgumentException("A share must expose at least one credential that belongs to this passport.", nameof(selectedCredentialIdentifiers));
        }

        var rawToken = ShareTokenGenerator.GenerateRawToken();
        var tokenHash = ShareTokenGenerator.Hash(rawToken);
        var share = passport.CreateShare(tokenHash, visibleFields, createdAtUtc, expiresAtUtc);
        return (share, rawToken);
    }

    public static PassportShare? Resolve(IEnumerable<PassportShare> candidates, string rawToken, DateTime nowUtc)
    {
        var tokenHash = ShareTokenGenerator.Hash(rawToken);
        var share = candidates.FirstOrDefault(s => s.TokenHash == tokenHash);
        if (share is null || share.IsRevoked || share.IsExpired(nowUtc))
        {
            return null;
        }

        return share;
    }
}
