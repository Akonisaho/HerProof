using HerProof.Domain.Common;

namespace HerProof.Domain.Identity;

/// <summary>What a recruiter or bursary partner is actually reaching out about - never a bare "like"
/// with no substance behind it. Both are real next steps in a hiring/funding process, not an
/// opportunity application (CandidateApplication) or a verification decision (CLAUDE.md rule 1).</summary>
public enum OutreachType
{
    CultureFitInterview,
    JobOffer,
}

/// <summary>Her real answer - never silently assumed. A culture-fit invite gets confirmed or declined
/// as "can attend" / "can't make it"; a job offer gets accepted or declined. Pending until she
/// actually responds - there is no default, and nothing here ever auto-advances on her behalf.</summary>
public enum OutreachResponseStatus
{
    Pending,
    Accepted,
    Declined,
}

/// <summary>A recruiter or bursary partner actually reaching out to a candidate's already-verified
/// profile - a real written message inviting her to a culture-fit interview or offering her the job,
/// independent of any specific opportunity or application, and her real response to it. Informational
/// only, like Notification, not gated by CLAUDE.md rule 1 since it records outreach and her answer
/// rather than a verification decision. Multiple rows per (recruiter, candidate) pair are expected and
/// legitimate - an interview invite today, a job offer weeks later, are both real, separate messages
/// worth keeping, each with its own response.</summary>
public class CandidateOutreach : Entity
{
    public Guid RecruiterUserId { get; init; }
    public Guid CandidateId { get; init; }
    public OutreachType OutreachType { get; init; }
    public string Message { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public OutreachResponseStatus ResponseStatus { get; private set; } = OutreachResponseStatus.Pending;
    public string? ResponseMessage { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public DateTime? RecruiterSeenAtUtc { get; private set; }

    private CandidateOutreach()
    {
        Message = string.Empty;
    }

    public CandidateOutreach(Guid recruiterUserId, Guid candidateId, OutreachType outreachType, string message, DateTime createdAtUtc)
    {
        RecruiterUserId = recruiterUserId;
        CandidateId = candidateId;
        OutreachType = outreachType;
        Message = message;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Answered once, honestly - a second call is a no-op rather than silently overwriting
    /// what she already said, the same append-only spirit as a verification decision.</summary>
    public void Respond(OutreachResponseStatus status, string? responseMessage, DateTime respondedAtUtc)
    {
        if (ResponseStatus != OutreachResponseStatus.Pending)
        {
            return;
        }

        ResponseStatus = status;
        ResponseMessage = responseMessage;
        RespondedAtUtc = respondedAtUtc;
    }

    /// <summary>Marks a real response as seen by the recruiter's own inbox - never set on a Pending
    /// outreach, since there is nothing yet to see.</summary>
    public void MarkSeenByRecruiter(DateTime seenAtUtc)
    {
        if (ResponseStatus == OutreachResponseStatus.Pending || RecruiterSeenAtUtc is not null)
        {
            return;
        }

        RecruiterSeenAtUtc = seenAtUtc;
    }
}
