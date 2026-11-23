using HerProof.Domain.Passports;
using HerProof.Domain.Verification;

namespace HerProof.Application.Passports;

/// <summary>The only place a PassportCredential may be issued (mirrors VerificationDecisionService's
/// single-construction-site pattern, CLAUDE.md rule 1): a credential can only ever come from a real
/// Verified VerificationRecord, never from any other status, and never twice for the same record.</summary>
public static class PassportIssuanceService
{
    /// <summary>Verified credentials are valid for 24 months before needing reassessment — an explicit,
    /// documented default (docs/05 P15 "validity periods and expiry"), not an unstated assumption.</summary>
    public static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(730);

    public static SkillsPassport EnsurePassport(SkillsPassport? existing, Guid candidateId, DateTime nowUtc)
    {
        if (existing is not null)
        {
            return existing;
        }

        var passportNumber = $"HP-{nowUtc:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        return new SkillsPassport(candidateId, passportNumber, nowUtc);
    }

    public static PassportCredential? IssueForVerification(
        SkillsPassport passport, VerificationRecord record, string competencySlug, DateTime nowUtc)
    {
        if (record.Decision != VerificationDecision.Verified)
        {
            return null;
        }

        if (passport.Credentials.Any(c => c.VerificationRecordId == record.Id))
        {
            return null;
        }

        var identifier = $"{passport.PassportNumber}-{competencySlug}-{record.Id.ToString("N")[..6]}".ToUpperInvariant();
        return passport.IssueCredential(record.Id, identifier, nowUtc, nowUtc.Add(DefaultValidity));
    }
}
