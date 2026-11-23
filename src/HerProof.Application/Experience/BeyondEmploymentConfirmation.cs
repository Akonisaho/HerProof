using HerProof.Domain.Competencies;

namespace HerProof.Application.Experience;

/// <summary>Confirming a Beyond Employment mapping only ever claims a competency — it never awards
/// or verifies one (docs/05 P06, CLAUDE.md working agreement). If the candidate already has a
/// CandidateCompetency row for this competency (claimed some other way, under assessment, or
/// already verified), confirming the mapping must not reset or downgrade it.</summary>
public static class BeyondEmploymentConfirmation
{
    public static CandidateCompetency EnsureClaimed(
        Guid candidateId,
        Guid competencyId,
        int? declaredLevel,
        CandidateCompetency? existing,
        DateTime nowUtc)
    {
        return existing ?? new CandidateCompetency(candidateId, competencyId, declaredLevel, nowUtc);
    }
}
