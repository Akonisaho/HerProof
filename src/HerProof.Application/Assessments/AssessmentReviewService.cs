using HerProof.Domain.Assessments;

namespace HerProof.Application.Assessments;

/// <summary>CLAUDE.md rule 1's sibling for practical-assessment reviews, same pattern as
/// VerificationDecisionService: the only place an AssessmentReview may be constructed, independent
/// of whatever the controller's own [Authorize] enforces - defence in depth, independently testable.
/// Closes a real gap found live: AssessmentReview existed on the domain model and was read by the
/// Assessor's evidence pack, but nothing anywhere ever actually created one - a human never had a
/// loop to review a candidate's submitted code, only the automated sandbox result.</summary>
public static class AssessmentReviewService
{
    public static AssessmentReview Create(
        bool actingUserIsAssessor,
        AssessmentSubmission submission,
        Guid assessorId,
        string criterionScoresJson,
        string feedback,
        string reviewStatus,
        DateTime nowUtc)
    {
        if (!actingUserIsAssessor)
        {
            throw new UnauthorizedAccessException("Only a user in role Assessor may review a practical assessment submission (CLAUDE.md rule 1).");
        }

        return submission.AddReview(assessorId, criterionScoresJson, feedback, reviewStatus, nowUtc);
    }
}
