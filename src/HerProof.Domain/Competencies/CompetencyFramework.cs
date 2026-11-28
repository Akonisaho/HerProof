using HerProof.Domain.Common;

namespace HerProof.Domain.Competencies;

public enum CompetencyCategory
{
    Software,
    NonTraditional,
}

public class Competency : Entity
{
    public string Name { get; private set; }
    public CompetencyCategory Category { get; private set; }
    public string FrameworkVersion { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Competency()
    {
        Name = string.Empty;
        FrameworkVersion = string.Empty;
    }

    public Competency(string name, CompetencyCategory category, string frameworkVersion)
    {
        Name = name;
        Category = category;
        FrameworkVersion = frameworkVersion;
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>Observable criteria for levels 0-4 of a competency (docs/02 seed data).</summary>
public class CompetencyLevelCriterion : Entity
{
    public Guid CompetencyId { get; init; }
    public int Level { get; init; }
    public string ObservableCriteria { get; init; }

    private CompetencyLevelCriterion()
    {
        ObservableCriteria = string.Empty;
    }

    public CompetencyLevelCriterion(Guid competencyId, int level, string observableCriteria)
    {
        if (level is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Competency levels run 0-4.");
        }

        CompetencyId = competencyId;
        Level = level;
        ObservableCriteria = observableCriteria;
    }
}

/// <summary>The never-zero rule (CLAUDE.md rule 2): VerifiedLevel is null until an assessor
/// decision sets it. Null must never be read as level 0 anywhere in the application layer.</summary>
public enum CandidateCompetencyStatus
{
    Claimed,
    EvidenceSubmitted,
    UnderAssessment,
    AssessmentCompleted,
    Verified,
    Developing,
    NotDemonstrated,
    Expired,
}

public class CandidateCompetency : Entity
{
    private readonly List<CompetencyEvidence> _evidence = [];

    public Guid CandidateId { get; init; }
    public Guid CompetencyId { get; init; }
    public int? DeclaredLevel { get; private set; }
    public int? VerifiedLevel { get; private set; }
    public CandidateCompetencyStatus Status { get; private set; } = CandidateCompetencyStatus.Claimed;
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<CompetencyEvidence> Evidence => _evidence;

    private CandidateCompetency()
    {
    }

    public CandidateCompetency(Guid candidateId, Guid competencyId, int? declaredLevel, DateTime nowUtc)
    {
        CandidateId = candidateId;
        CompetencyId = competencyId;
        DeclaredLevel = declaredLevel;
        UpdatedAtUtc = nowUtc;
    }

    public CompetencyEvidence AddEvidence(string evidenceType, string evidenceReference, string? description, DateTime nowUtc)
    {
        var evidence = new CompetencyEvidence(Id, evidenceType, evidenceReference, description, nowUtc);
        _evidence.Add(evidence);
        Status = CandidateCompetencyStatus.EvidenceSubmitted;
        UpdatedAtUtc = nowUtc;
        return evidence;
    }

    public void BeginAssessment(DateTime nowUtc)
    {
        Status = CandidateCompetencyStatus.UnderAssessment;
        UpdatedAtUtc = nowUtc;
    }

    public void CompleteAssessment(DateTime nowUtc)
    {
        Status = CandidateCompetencyStatus.AssessmentCompleted;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Only the verification workflow (an Assessor decision, docs/01 section 9) may call this.
    /// Application layer must reject any caller that is not in role Assessor before reaching here.</summary>
    public void ApplyAssessorDecision(CandidateCompetencyStatus decision, int? finalLevel, DateTime nowUtc)
    {
        if (decision is not (CandidateCompetencyStatus.Verified or CandidateCompetencyStatus.Developing or CandidateCompetencyStatus.NotDemonstrated))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Not a valid assessor decision.");
        }

        Status = decision;
        VerifiedLevel = decision == CandidateCompetencyStatus.Verified ? finalLevel : VerifiedLevel;
        UpdatedAtUtc = nowUtc;
    }

    public void Expire(DateTime nowUtc)
    {
        Status = CandidateCompetencyStatus.Expired;
        UpdatedAtUtc = nowUtc;
    }
}

public class CompetencyEvidence : Entity
{
    public Guid CandidateCompetencyId { get; init; }
    public string EvidenceType { get; init; }
    public string EvidenceReference { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    private CompetencyEvidence()
    {
        EvidenceType = string.Empty;
        EvidenceReference = string.Empty;
    }

    public CompetencyEvidence(Guid candidateCompetencyId, string evidenceType, string evidenceReference, string? description, DateTime createdAtUtc)
    {
        CandidateCompetencyId = candidateCompetencyId;
        EvidenceType = evidenceType;
        EvidenceReference = evidenceReference;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }
}

public enum LearningProgressStatus
{
    NotStarted,
    InProgress,
    Completed,
}

/// <summary>Candidate-reported progress against a recommended learning resource (docs/05 P16).
/// This never feeds verification — it's visible to the candidate only, to keep a gap-closing plan
/// in view. Finishing a resource does not verify anything; only an assessor decision can.</summary>
public class LearningProgress : Entity
{
    public Guid CandidateId { get; init; }
    public Guid LearningResourceId { get; init; }
    public LearningProgressStatus Status { get; private set; } = LearningProgressStatus.NotStarted;
    public DateTime UpdatedAtUtc { get; private set; }

    private LearningProgress()
    {
    }

    public LearningProgress(Guid candidateId, Guid learningResourceId, DateTime nowUtc)
    {
        CandidateId = candidateId;
        LearningResourceId = learningResourceId;
        UpdatedAtUtc = nowUtc;
    }

    public void SetStatus(LearningProgressStatus status, DateTime nowUtc)
    {
        Status = status;
        UpdatedAtUtc = nowUtc;
    }
}

public class LearningResource : Entity
{
    public Guid CompetencyId { get; init; }
    public string Title { get; init; }
    public string Url { get; init; }
    public string CostType { get; init; }
    public int TargetLevel { get; init; }

    private LearningResource()
    {
        Title = string.Empty;
        Url = string.Empty;
        CostType = string.Empty;
    }

    public LearningResource(Guid competencyId, string title, string url, string costType, int targetLevel)
    {
        CompetencyId = competencyId;
        Title = title;
        Url = url;
        CostType = costType;
        TargetLevel = targetLevel;
    }
}

/// <summary>"Languages/competencies I want to be tested on" - a candidate's own declared interest,
/// independent of any specific opportunity. Purely informational (CLAUDE.md rule 2 doesn't apply
/// here: this is not evidence of anything, just a preference the candidate can act on).</summary>
public class CandidatePreferredCompetency : Entity
{
    public Guid CandidateId { get; init; }
    public Guid CompetencyId { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    private CandidatePreferredCompetency()
    {
    }

    public CandidatePreferredCompetency(Guid candidateId, Guid competencyId, DateTime createdAtUtc)
    {
        CandidateId = candidateId;
        CompetencyId = competencyId;
        CreatedAtUtc = createdAtUtc;
    }
}
