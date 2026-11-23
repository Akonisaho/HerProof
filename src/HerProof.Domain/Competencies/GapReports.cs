using HerProof.Domain.Common;

namespace HerProof.Domain.Competencies;

/// <summary>CLAUDE.md rule 2 (non-negotiable): an unassessed competency is NEVER scored as zero.
/// NoEvidence and ClaimedNotAssessed are distinct from NotDemonstrated.</summary>
public enum EvidenceState
{
    Demonstrated,
    ClaimedNotAssessed,
    NotDemonstrated,
    NoEvidence,
}

public class GapReport : Entity
{
    private readonly List<GapReportItem> _items = [];

    public Guid CandidateId { get; init; }
    public Guid OpportunityId { get; init; }
    public DateTime GeneratedAtUtc { get; init; }

    public IReadOnlyCollection<GapReportItem> Items => _items;

    private GapReport()
    {
    }

    public GapReport(Guid candidateId, Guid opportunityId, DateTime generatedAtUtc)
    {
        CandidateId = candidateId;
        OpportunityId = opportunityId;
        GeneratedAtUtc = generatedAtUtc;
    }

    public GapReportItem AddItem(Guid competencyId, int requiredLevel, int? demonstratedLevel, EvidenceState evidenceState, string explanation, Guid? learningResourceId)
    {
        if (evidenceState != EvidenceState.Demonstrated && demonstratedLevel is not null)
        {
            throw new InvalidOperationException("A demonstrated_level can only be set when evidence_state is Demonstrated.");
        }

        var item = new GapReportItem(Id, competencyId, requiredLevel, demonstratedLevel, evidenceState, explanation, learningResourceId);
        _items.Add(item);
        return item;
    }
}

public class GapReportItem : Entity
{
    public Guid GapReportId { get; init; }
    public Guid CompetencyId { get; init; }
    public int RequiredLevel { get; init; }

    /// <summary>Null means no assessed evidence. Never read as level 0 (CLAUDE.md rule 2).</summary>
    public int? DemonstratedLevel { get; init; }

    public EvidenceState EvidenceState { get; init; }
    public string Explanation { get; init; }
    public Guid? LearningResourceId { get; init; }

    private GapReportItem()
    {
        Explanation = string.Empty;
    }

    public GapReportItem(Guid gapReportId, Guid competencyId, int requiredLevel, int? demonstratedLevel, EvidenceState evidenceState, string explanation, Guid? learningResourceId)
    {
        GapReportId = gapReportId;
        CompetencyId = competencyId;
        RequiredLevel = requiredLevel;
        DemonstratedLevel = demonstratedLevel;
        EvidenceState = evidenceState;
        Explanation = explanation;
        LearningResourceId = learningResourceId;
    }
}
