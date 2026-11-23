using System.ComponentModel.DataAnnotations;

namespace HerProof.Web.Models.Account;

/// <summary>Candidate self-service profile editing (docs/05 P04's own eligibility-rule fields,
/// finally wired up to a real form instead of the read-only stub CandidateProfileController started
/// with). Every field is optional: "not provided" must stay Unknown, never a failure (CLAUDE.md rule 2
/// applied to eligibility, not just competency evidence).</summary>
public class EditProfileViewModel
{
    public string? Province { get; set; }
    public string? EducationLevel { get; set; }
    public string? GenderDeclared { get; set; }
    public string? HouseholdIncomeBand { get; set; }
    public string? CareerInterests { get; set; }
    public string? GitHubProfile { get; set; }
    public string? LinkedInProfile { get; set; }
    public string? FieldOfStudy { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }
}
