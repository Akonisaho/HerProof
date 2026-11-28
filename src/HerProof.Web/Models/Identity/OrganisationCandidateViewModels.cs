using HerProof.Domain.Identity;

namespace HerProof.Web.Models.Identity;

/// <summary>One candidate as a Recruiter or Bursary Partner sees her, browsing independent of any
/// opportunity - only what's safe to share with an employer (CLAUDE.md rule 9): verified competencies,
/// submitted-repository count as a project-activity signal, and the profile fields she filled in
/// herself for eligibility/interest. Never repositories' contents, recordings, transcripts, integrity
/// events, scores, or contact details.</summary>
public class OrganisationCandidateListItem
{
    public required Guid CandidateId { get; init; }
    public required string FullName { get; init; }
    public string? Province { get; init; }
    public string? EducationLevel { get; init; }
    public string? CareerInterests { get; init; }
    public string? GitHubProfile { get; init; }
    public string? LinkedInProfile { get; init; }
    public required IReadOnlyList<(string CompetencyName, int? Level)> VerifiedCompetencies { get; init; }
    public required IReadOnlyList<string> PreferredCompetencyNames { get; init; }
    public required int RepositoryCount { get; init; }

    /// <summary>Every real message this recruiter has already sent her, newest first, with her real
    /// answer if she's given one - never just a yes/no toggle, since a second, different message (an
    /// offer after an earlier interview invite) is a real, separate, legitimate thing to send.</summary>
    public required IReadOnlyList<(OutreachType Type, DateTime SentAtUtc, OutreachResponseStatus ResponseStatus)> OutreachSent { get; init; }
}

/// <summary>One row of the recruiter/partner's own "mailbox" (OrganisationCandidatesController.Responses)
/// - a real response she gave to a real outreach message, never a bare like.</summary>
public class OrganisationResponseRow
{
    public required Guid CandidateId { get; init; }
    public required string CandidateName { get; init; }
    public required OutreachType OutreachType { get; init; }
    public required string Message { get; init; }
    public required DateTime SentAtUtc { get; init; }
    public required OutreachResponseStatus ResponseStatus { get; init; }
    public string? ResponseMessage { get; init; }
    public DateTime? RespondedAtUtc { get; init; }
}
