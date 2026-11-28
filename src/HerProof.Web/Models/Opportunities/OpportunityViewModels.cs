using System.ComponentModel.DataAnnotations;
using HerProof.Domain.Opportunities;

namespace HerProof.Web.Models.Opportunities;

public class CreateOpportunityViewModel
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Type")]
    public OpportunityType OpportunityType { get; set; }

    public string? Location { get; set; }

    [Display(Name = "Application deadline")]
    [DataType(DataType.Date)]
    public DateTime? DeadlineUtc { get; set; }

    [Display(Name = "Salary (minimum)")]
    [Range(0, double.MaxValue, ErrorMessage = "Salary cannot be negative.")]
    public decimal? SalaryMin { get; set; }

    [Display(Name = "Salary (maximum)")]
    [Range(0, double.MaxValue, ErrorMessage = "Salary cannot be negative.")]
    public decimal? SalaryMax { get; set; }

    [Display(Name = "Years of experience required")]
    [Range(0, 60, ErrorMessage = "Enter a realistic number of years.")]
    public int? ExperienceYearsRequired { get; set; }
}

public class OpportunitySearchViewModel
{
    [Display(Name = "Search")]
    public string? Q { get; set; }

    public OpportunityType? Type { get; set; }

    public string? Location { get; set; }

    [Display(Name = "Max years of experience")]
    [Range(0, 60)]
    public int? MaxExperienceYears { get; set; }

    [Display(Name = "Only show salary disclosed")]
    public bool HasSalaryDisclosed { get; set; }
}

public class AddCompetencyRequirementViewModel
{
    [Required]
    [Display(Name = "Competency")]
    public Guid CompetencyId { get; set; }

    [Range(0, 4)]
    [Display(Name = "Required level")]
    public int RequiredLevel { get; set; }

    [Display(Name = "Mandatory")]
    public bool IsMandatory { get; set; }

    public List<(Guid Id, string Name)> AvailableCompetencies { get; set; } = [];
}

public class AddEligibilityRuleViewModel
{
    [Required]
    [Display(Name = "Rule type")]
    public EligibilityRuleType RuleType { get; set; }

    [Required]
    public EligibilityOperator Operator { get; set; }

    [Required]
    [Display(Name = "Value (or \"Any\")")]
    public string Value { get; set; } = string.Empty;

    [Display(Name = "Hard filter (blocks application if not met)")]
    public bool IsHardFilter { get; set; }

    [Required]
    [Display(Name = "Explanation shown to the candidate")]
    public string Explanation { get; set; } = string.Empty;
}

public class OpportunityListItem
{
    public required Opportunity Opportunity { get; init; }
    public required string OrganisationName { get; init; }
}

public class OrganisationOpportunityDetailViewModel
{
    public required Opportunity Opportunity { get; init; }
    public required IReadOnlyList<(Guid Id, string Name)> AvailableCompetencies { get; init; }
    public required IReadOnlyDictionary<Guid, string> CompetencyNames { get; init; }

    /// <summary>This recruiter/partner's own Published assessments - only a Published one can be
    /// linked, so a candidate who clicks "take the test" never lands on something unfinished.</summary>
    public required IReadOnlyList<(Guid Id, string Title, string AssessmentType, int DurationMinutes)> OwnPublishedAssessments { get; init; }

    public (string Title, string AssessmentType, int DurationMinutes)? LinkedAssessment { get; init; }
}

/// <summary>Partner/recruiter review of one application (docs/05 P16): eligibility and the gap
/// report are shown exactly as the candidate saw them — no separate, opaque scoring model — plus
/// whichever verified passport credentials the candidate chose to attach.</summary>
public class ApplicationReviewItem
{
    public required CandidateApplication Application { get; init; }
    public required string CandidateName { get; init; }
    public required IReadOnlyList<(string CredentialIdentifier, string CompetencyName)> AttachedCredentials { get; init; }

    /// <summary>The same public, read-only certificate page the candidate's own QR code opens — never a
    /// direct view of her repositories, recordings, transcripts or scores (CLAUDE.md rule 9). Null until
    /// she has a Skills Passport at all.</summary>
    public string? PassportCertificateUrl { get; init; }

    /// <summary>Null when the candidate's profile couldn't be resolved at all; an empty rule list
    /// inside it (not null) means the opportunity simply has no eligibility rules to show.</summary>
    public HerProof.Application.Opportunities.OpportunityEligibilitySummary? Eligibility { get; init; }
}

public class OrganisationApplicationsViewModel
{
    public required Opportunity Opportunity { get; init; }
    public required IReadOnlyList<ApplicationReviewItem> Applications { get; init; }
}

/// <summary>The one landing page a Recruiter or Partner actually lands on - an overview across every
/// opportunity they own, not one opportunity at a time.</summary>
public class OrganisationDashboardViewModel
{
    public required Guid OrganisationId { get; init; }
    public required string OrganisationName { get; init; }
    public required bool IsBursaryPartner { get; init; }
    public required int PublishedCount { get; init; }
    public required int TotalOpportunityCount { get; init; }
    public required int TotalPendingApplications { get; init; }
    public required int ReachedOutCandidateCount { get; init; }
    public required IReadOnlyList<OrganisationDashboardOpportunityRow> Opportunities { get; init; }
}

public class OrganisationDashboardOpportunityRow
{
    public required Guid OpportunityId { get; init; }
    public required string Title { get; init; }
    public required string OpportunityType { get; init; }
    public required string Status { get; init; }
    public required int TotalApplications { get; init; }
    public required int PendingApplications { get; init; }
}
