using HerProof.Domain.Competencies;
using HerProof.Domain.Passports;

namespace HerProof.Web.Models.Passports;

/// <summary>Candidate's own dashboard view (docs/05 P15): verified credentials and unverified/in-progress
/// competencies are kept in separate, clearly labelled lists — never merged into one "skills" list.</summary>
public class PassportDashboardViewModel
{
    public SkillsPassport? Passport { get; init; }
    public required IReadOnlyList<(PassportCredential Credential, string CompetencyName)> VerifiedCredentials { get; init; }
    public required IReadOnlyList<(CandidateCompetency CandidateCompetency, string CompetencyName)> UnverifiedCompetencies { get; init; }
    public required IReadOnlyList<PassportShare> ActiveShares { get; init; }
    public required IReadOnlyList<PassportShare> RevokedOrExpiredShares { get; init; }
}

public class CreateShareViewModel
{
    public List<string> SelectedCredentialIdentifiers { get; set; } = [];
    public int? ExpiresInDays { get; set; }
}

public class ShareCreatedViewModel
{
    public required string PublicVerifyUrl { get; init; }
    public required Guid ShareId { get; init; }
}

/// <summary>The public verify page's only data shape. It is built exclusively from
/// PassportShare.VisibleFields (credential identifiers the candidate chose to expose) — there is no
/// code path here that can reach recordings, transcripts, repos, contact details, integrity events
/// or scores, because this type never carries them (CLAUDE.md rule 9).</summary>
public class PublicPassportViewModel
{
    public required string PassportNumber { get; init; }
    public required string CandidateName { get; init; }
    public required IReadOnlyList<PublicCredentialViewModel> Credentials { get; init; }
}

public class PublicCredentialViewModel
{
    public required string CompetencyName { get; init; }
    public required string CredentialIdentifier { get; init; }
    public required DateTime IssuedAtUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public required string Status { get; init; }
}
