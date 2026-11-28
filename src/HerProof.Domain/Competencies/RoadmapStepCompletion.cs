using HerProof.Domain.Common;

namespace HerProof.Domain.Competencies;

/// <summary>Marks one step of a candidate's "Not sure where to start?" career roadmap complete -
/// informational only, like Notification/CandidateOutreach, never gating anything CLAUDE.md rule 1
/// protects (it never touches CandidateCompetencyStatus.Verified - it only ever reads a decision
/// already made by VerificationDecisionService elsewhere). Locking is derived from the
/// presence/absence of these rows, not stored separately: step N is unlocked once steps 0..N-1 each
/// have one of these. A step completes via either real path: an automated sandbox pass
/// (AssessmentSessionId set, VerifiedByAssessor false) or a human Assessor's own Verified decision for
/// that competency (AssessmentSessionId null - she may never have attempted a practical assessment for
/// it at all, e.g. evidence was only a repository + interview - VerifiedByAssessor true).</summary>
public class RoadmapStepCompletion : Entity
{
    public Guid CandidateId { get; init; }
    public string Goal { get; init; }
    public Guid CompetencyId { get; init; }
    public Guid? AssessmentSessionId { get; init; }
    public bool VerifiedByAssessor { get; init; }
    public DateTime CompletedAtUtc { get; init; }

    private RoadmapStepCompletion()
    {
        Goal = string.Empty;
    }

    public RoadmapStepCompletion(Guid candidateId, string goal, Guid competencyId, Guid? assessmentSessionId, bool verifiedByAssessor, DateTime completedAtUtc)
    {
        CandidateId = candidateId;
        Goal = goal;
        CompetencyId = competencyId;
        AssessmentSessionId = assessmentSessionId;
        VerifiedByAssessor = verifiedByAssessor;
        CompletedAtUtc = completedAtUtc;
    }
}
