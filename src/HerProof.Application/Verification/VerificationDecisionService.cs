using HerProof.Domain.Verification;

namespace HerProof.Application.Verification;

/// <summary>CLAUDE.md rule 1, the whole product's core promise: "No code path (AI, score threshold,
/// background job, admin script) may set status Verified without an Assessor decision." This is the
/// one and only place a VerificationRecord may be constructed — every caller, including the Web
/// controller (which also gates the endpoint with [Authorize(Roles="Assessor")] as defence in depth),
/// must pass through here, where the rule is enforced and independently testable.</summary>
public static class VerificationDecisionService
{
    public static VerificationRecord Create(
        bool actingUserIsAssessor,
        Guid candidateId,
        Guid competencyId,
        Guid assessmentId,
        int? finalLevel,
        Guid assessorId,
        VerificationDecision decision,
        string evidenceSummary,
        DateTime nowUtc,
        Guid? supersedesId)
    {
        if (!actingUserIsAssessor)
        {
            throw new UnauthorizedAccessException("Only a user in role Assessor may create a verification record (CLAUDE.md rule 1).");
        }

        return new VerificationRecord(candidateId, competencyId, assessmentId, finalLevel, assessorId, decision, evidenceSummary, nowUtc, supersedesId);
    }
}
