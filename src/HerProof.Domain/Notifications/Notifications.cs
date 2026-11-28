using HerProof.Domain.Common;

namespace HerProof.Domain.Notifications;

public enum NotificationType
{
    CompetencyVerified,
    CompetencyNotDemonstrated,
    RecruiterInterest,
    NewOpportunity,
    CultureFitInterviewInvite,
    JobOffer,
}

/// <summary>Informational only - unlike VerificationRecord, nothing here is gated by CLAUDE.md rule 1,
/// so any caller may construct one directly once the real event (an Assessor decision, a recruiter's
/// outreach) has already happened.</summary>
public class Notification : Entity
{
    public Guid CandidateId { get; init; }
    public NotificationType NotificationType { get; init; }
    public string Message { get; init; }
    public Guid? RelatedCompetencyId { get; init; }

    /// <summary>Set only for CultureFitInterviewInvite/JobOffer - lets the Notifications page show
    /// real Accept/Decline buttons wired to the exact CandidateOutreach this notification is about,
    /// instead of leaving her no way to respond to it at all.</summary>
    public Guid? RelatedOutreachId { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ReadAtUtc { get; private set; }

    private Notification()
    {
        Message = string.Empty;
    }

    public Notification(Guid candidateId, NotificationType notificationType, string message, Guid? relatedCompetencyId, DateTime createdAtUtc, Guid? relatedOutreachId = null)
    {
        CandidateId = candidateId;
        NotificationType = notificationType;
        Message = message;
        RelatedCompetencyId = relatedCompetencyId;
        RelatedOutreachId = relatedOutreachId;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;
}
