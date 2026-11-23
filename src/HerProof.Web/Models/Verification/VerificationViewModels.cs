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
    public required IReadOnlyList<ExecutionReport> ExecutionReports { get; init; }

    public required IReadOnlyList<(InterviewQuestion Question, InterviewTurn Turn)> InterviewTurns { get; init; }

    public required IReadOnlyList<AssessmentReview> AssessmentReviews { get; init; }
    public required IReadOnlyList<IntegrityEvent> IntegrityTimeline { get; init; }

    public required IReadOnlyList<VerificationRecord> DecisionHistory { get; init; }
    public required HerProof.Application.Verification.AiSuggestion AiSuggestion { get; init; }
}

public class DecideViewModel
{
    public VerificationDecision Decision { get; set; }
    public int? FinalLevel { get; set; }
    public string Feedback { get; set; } = string.Empty;
}
