using HerProof.Domain.Partnerships;

namespace HerProof.Domain.Tests;

public class PartnershipInquiryTests
{
    [Fact]
    public void New_inquiry_starts_in_New_status()
    {
        var inquiry = new PartnershipInquiry(
            "Acme Corp",
            "Jane Doe",
            "jane@acme.test",
            "0821234567",
            PartnershipInquiryType.HiringPartner,
            "We'd like to hire junior developers.",
            new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PartnershipInquiryStatus.New, inquiry.Status);
        Assert.Equal("Acme Corp", inquiry.OrganisationName);
        Assert.Equal("jane@acme.test", inquiry.ContactEmail);
    }

    [Fact]
    public void MarkContacted_moves_status_from_New_to_Contacted()
    {
        var inquiry = new PartnershipInquiry(
            "Acme Corp", "Jane Doe", "jane@acme.test", null,
            PartnershipInquiryType.BursaryProvider, "message",
            DateTime.UtcNow);

        inquiry.MarkContacted();

        Assert.Equal(PartnershipInquiryStatus.Contacted, inquiry.Status);
    }

    [Fact]
    public void MarkClosed_moves_status_to_Closed()
    {
        var inquiry = new PartnershipInquiry(
            "Acme Corp", "Jane Doe", "jane@acme.test", null,
            PartnershipInquiryType.Other, "message",
            DateTime.UtcNow);

        inquiry.MarkClosed();

        Assert.Equal(PartnershipInquiryStatus.Closed, inquiry.Status);
    }
}
