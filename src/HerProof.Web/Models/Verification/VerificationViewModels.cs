using HerProof.Domain.Assessments;
using HerProof.Domain.Interviews;
using HerProof.Domain.Repositories;
using HerProof.Domain.Verification;

namespace HerProof.Web.Models.Verification;

/// <summary>The evidence pack (docs/05 P14): everything an assessor needs for one candidate +
/// competency, in one place. Assembled read-only from across the pipeline — it creates nothing.</summary>
public class EvidencePackViewModel
{
    public required string CandidateName { get; init; }
    public required string CompetencyName { get; init; }
    public int? DeclaredLevel { get; init; }
    public int? VerifiedLevel { get; init; }
    public required string CandidateCompetencyStatus { get; init; }

    public required IReadOnlyList<RepositoryFinding> RepositoryFindings { get; init; }
    public required IReadOnlyDictionary<Guid, string?> FindingSourceLinks { get; init; }
    public required IReadOnlyList<RepositorySummaryRow> RelevantRepositories { get; init; }
    public required IReadOnlyList<ExecutionReport> ExecutionReports { get; init; }

    public required IReadOnlyList<(InterviewQuestion Question, InterviewTurn Turn)> InterviewTurns { get; init; }
    public required IReadOnlyDictionary<Guid, string?> QuestionSourceLinks { get; init; }

    public required IReadOnlyList<AssessmentReview> AssessmentReviews { get; init; }
    public required IReadOnlyList<PendingSubmissionRow> PendingSubmissions { get; init; }
    public required IReadOnlyList<IntegrityEvent> IntegrityTimeline { get; init; }

    public required IReadOnlyList<VerificationRecord> DecisionHistory { get; init; }
    public required HerProof.Application.Verification.AiSuggestion AiSuggestion { get; init; }
}

/// <summary>What repository actually backed this competency's findings - the Evidence Pack previously
/// buried this inside individual file:line references and never named the repository itself.</summary>
public class RepositorySummaryRow
{
    public required string RepositoryName { get; init; }
    public required string RepositoryUrl { get; init; }
    public required string Branch { get; init; }
    public required string Visibility { get; init; }
    public required string ContributorStatus { get; init; }
}

public class DecideViewModel
{
    public VerificationDecision Decision { get; set; }
    public int? FinalLevel { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>"All of her assessments in one folder" - one candidate, every competency she's touched
/// (whatever its status) plus her issued Skills Passport credentials, in one place, instead of
/// hunting through separate inbox rows per competency.</summary>
public class CandidateOverviewViewModel
{
    public required Guid CandidateId { get; init; }
    public required string CandidateName { get; init; }
    public required IReadOnlyList<CandidateOverviewCompetencyRow> Competencies { get; init; }
    public required string? PassportNumber { get; init; }
    public required IReadOnlyList<CandidateOverviewCredentialRow> Credentials { get; init; }
}

public class CandidateOverviewCompetencyRow
{
    public required Guid CompetencyId { get; init; }
    public required string CompetencyName { get; init; }
    public required string Status { get; init; }
    public int? DeclaredLevel { get; init; }
    public int? VerifiedLevel { get; init; }
}

public class CandidateOverviewCredentialRow
{
    public required string CompetencyName { get; init; }
    public required int? FinalLevel { get; init; }
    public required DateTime IssuedAtUtc { get; init; }
    public required string CredentialStatus { get; init; }
}

/// <summary>One card per candidate on the assessor inbox, not one card per competency - however many
/// competencies she has pending, her name appears once and the whole thing opens into her
/// <see cref="CandidateOverviewViewModel"/> folder, the same way an opportunity card lists once per
/// opportunity even though it has several applicants underneath.</summary>
public class InboxCandidateRow
{
    public required Guid CandidateId { get; init; }
    public required string CandidateName { get; init; }
    public required int PendingCount { get; init; }
    public required int UnreadCount { get; init; }
    public required DateTime MostRecentUpdateUtc { get; init; }
    public required IReadOnlyList<string> CompetencyNames { get; init; }
}

/// <summary>One distinct pending practical-assessment submission waiting on a human review - carries
/// its own timestamp and the sandbox's own automated pass/fail readout, so a list of these never
/// collapses into N identical unlabelled "Review submitted code" buttons.</summary>
public class PendingSubmissionRow
{
    public required Guid SubmissionId { get; init; }
    public required DateTime SubmittedAtUtc { get; init; }
    public required string AutomatedSummary { get; init; }
}

/// <summary>One real email, not a folder: the exact candidate + competency a submission is about, and
/// where clicking it takes you - straight into that evidence pack, same as opening a specific email.</summary>
public class InboxMessageRow
{
    public required Guid CandidateId { get; init; }
    public required Guid CompetencyId { get; init; }
    public required string CandidateName { get; init; }
    public required string CompetencyName { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required bool IsUnread { get; init; }
}
