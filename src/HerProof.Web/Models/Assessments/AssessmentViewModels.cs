using System.ComponentModel.DataAnnotations;

namespace HerProof.Web.Models.Assessments;

public class CreateAssessmentViewModel
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Type")]
    public string AssessmentType { get; set; } = "Practical";

    [Range(5, 480)]
    [Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 60;
}

public class AddAssessmentCompetencyViewModel
{
    [Required]
    [Display(Name = "Competency")]
    public Guid CompetencyId { get; set; }

    [Range(0.1, 10)]
    public decimal Weight { get; set; } = 1m;
}

public class AddRubricCriterionViewModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(0.1, 10)]
    public decimal Weight { get; set; } = 1m;

    [Required]
    public string Descriptor { get; set; } = string.Empty;
}

public class SubmitWorkViewModel
{
    [Required]
    [StringLength(200_000, ErrorMessage = "Submission is too large (max 200,000 characters).")]
    [Display(Name = "Your solution")]
    public string Code { get; set; } = string.Empty;
}
