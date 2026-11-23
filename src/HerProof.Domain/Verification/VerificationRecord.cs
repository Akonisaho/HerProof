using HerProof.Domain.Common;

namespace HerProof.Domain.Verification;

public enum VerificationDecision
{
    Verified,
    Developing,
    NotDemonstrated,
}

/// <summary>Append-only (docs/02 rule 1, ADR-07). CLAUDE.md rule 1 and rule 8: only an authorised
/// Assessor can create this, and a new decision never edits an old one — it inserts a new row with
/// SupersedesId. The Application layer must reject any other caller before reaching here, and a
/// domain-level test must prove that rejection (docs/05 P14).</summary>
public class VerificationRecord : Entity, IAppendOnly
{
    public Guid CandidateId { get; init; }
    public Guid CompetencyId { get; init; }
    public Guid AssessmentId { get; init; }
    public int? FinalLevel { get; init; }
    public Guid AssessorId { get; init; }
    public VerificationDecision Decision { get; init; }
    public string EvidenceSummary { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? SupersedesId { get; init; }

    private VerificationRecord()
    {
        EvidenceSummary = string.Empty;
    }

    public VerificationRecord(
        Guid candidateId,
        Guid competencyId,
        Guid assessmentId,
        int? finalLevel,
        Guid assessorId,
        VerificationDecision decision,
        string evidenceSummary,
        DateTime createdAtUtc,
        Guid? supersedesId)
    {
        if (decision == VerificationDecision.Verified && finalLevel is null)
        {
            throw new ArgumentException("A Verified decision must record a final level.", nameof(finalLevel));
        }

        CandidateId = candidateId;
        CompetencyId = competencyId;
        AssessmentId = assessmentId;
        FinalLevel = finalLevel;
        AssessorId = assessorId;
        Decision = decision;
        EvidenceSummary = evidenceSummary;
        CreatedAtUtc = createdAtUtc;
        SupersedesId = supersedesId;
    }
}
