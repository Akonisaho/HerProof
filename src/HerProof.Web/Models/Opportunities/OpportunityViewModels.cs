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

public class OrganisationOpportunityDetailViewModel
{
    public required Opportunity Opportunity { get; init; }
    public required IReadOnlyList<(Guid Id, string Name)> AvailableCompetencies { get; init; }
    public required IReadOnlyDictionary<Guid, string> CompetencyNames { get; init; }
}

/// <summary>Partner/recruiter review of one application (docs/05 P16): eligibility and the gap
/// report are shown exactly as the candidate saw them — no separate, opaque scoring model — plus
/// whichever verified passport credentials the candidate chose to attach.</summary>
public class ApplicationReviewItem
{
    public required CandidateApplication Application { get; init; }
    public required string CandidateName { get; init; }
    public required IReadOnlyList<(string CredentialIdentifier, string CompetencyName)> AttachedCredentials { get; init; }
}

public class OrganisationApplicationsViewModel
{
    public required Opportunity Opportunity { get; init; }
    public required IReadOnlyList<ApplicationReviewItem> Applications { get; init; }
}
