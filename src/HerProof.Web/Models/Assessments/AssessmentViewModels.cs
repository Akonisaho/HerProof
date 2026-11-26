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
    public string AssessmentType { get; set; } = "C#";

    [Range(5, 480)]
    [Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 90;
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
    /// <summary>Legacy single-file path: a freeform assessment with no structured Questions.</summary>
    [StringLength(200_000, ErrorMessage = "Submission is too large (max 200,000 characters).")]
    [Display(Name = "Your solution")]
    public string? Code { get; set; }

    /// <summary>A structured (e.g. C#) assessment: one Monaco editor per question, serialized
    /// client-side into this JSON array of {fileName, content} before the form posts.</summary>
    [StringLength(400_000, ErrorMessage = "Submission is too large.")]
    public string? FilesJson { get; set; }
}
