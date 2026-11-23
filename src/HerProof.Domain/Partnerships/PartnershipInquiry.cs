using HerProof.Domain.Common;

namespace HerProof.Domain.Partnerships;

public enum PartnershipInquiryType
{
    HiringPartner,
    BursaryProvider,
    Other,
}

public enum PartnershipInquiryStatus
{
    New,
    Contacted,
    Closed,
}

/// <summary>Real "Partner with us" intake (docs/STUBS.md HP-033, closed): previously just a
/// mailto: link in the footer with no record of who asked. Append-only lead data, not a verification
/// or decision record, so updates to Status are allowed.</summary>
public class PartnershipInquiry : Entity
{
    public string OrganisationName { get; private set; }
    public string ContactName { get; private set; }
    public string ContactEmail { get; private set; }
    public string? Phone { get; private set; }
    public PartnershipInquiryType InquiryType { get; private set; }
    public string Message { get; private set; }
    public PartnershipInquiryStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; init; }

    private PartnershipInquiry()
    {
        OrganisationName = string.Empty;
        ContactName = string.Empty;
        ContactEmail = string.Empty;
        Message = string.Empty;
    }

    public PartnershipInquiry(
        string organisationName,
        string contactName,
        string contactEmail,
        string? phone,
        PartnershipInquiryType inquiryType,
        string message,
        DateTime createdAtUtc)
    {
        OrganisationName = organisationName;
        ContactName = contactName;
        ContactEmail = contactEmail;
        Phone = phone;
        InquiryType = inquiryType;
        Message = message;
        Status = PartnershipInquiryStatus.New;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkContacted() => Status = PartnershipInquiryStatus.Contacted;

    public void MarkClosed() => Status = PartnershipInquiryStatus.Closed;
}
