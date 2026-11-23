using System.ComponentModel.DataAnnotations;
using HerProof.Domain.Partnerships;

namespace HerProof.Web.Models.Partnerships;

public class PartnerInquiryViewModel
{
    [Required]
    [Display(Name = "Organisation name")]
    public string OrganisationName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Your name")]
    public string ContactName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string ContactEmail { get; set; } = string.Empty;

    [Display(Name = "Phone (optional)")]
    public string? Phone { get; set; }

    [Required]
    [Display(Name = "I'm interested in")]
    public PartnershipInquiryType InquiryType { get; set; } = PartnershipInquiryType.HiringPartner;

    [Required]
    [Display(Name = "Tell us a bit more")]
    public string Message { get; set; } = string.Empty;
}
