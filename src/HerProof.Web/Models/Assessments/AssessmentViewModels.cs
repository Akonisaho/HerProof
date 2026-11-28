using System.ComponentModel.DataAnnotations;
using HerProof.Domain.Assessments;

namespace HerProof.Web.Models.Assessments;

/// <summary>The practical-assessment review (closes the human-review gap found live: the Assessor's
/// evidence pack showed AssessmentReview rows, but nothing ever created one). Shows the candidate's
/// actually-submitted code, question by question, next to its hidden test (never shown to her) and
/// the automated sandbox result — the human reviews real evidence here, not a summary of it.</summary>
public class SubmissionReviewViewModel
{
    public required Guid SubmissionId { get; init; }
    public required string CandidateName { get; init; }
    public required string AssessmentTitle { get; init; }
    public required IReadOnlyList<SubmissionQuestionViewModel> Questions { get; init; }
    public string? TestResultsJson { get; init; }
    public AssessmentReview? ExistingReview { get; init; }
}

public class SubmissionQuestionViewModel
{
    public required string Title { get; init; }
    public required string Prompt { get; init; }
    public required string FileName { get; init; }
    public string? HiddenTestCode { get; init; }
    public string? SubmittedContent { get; init; }
}

public class SubmitReviewViewModel
{
    [Required]
    public string ReviewStatus { get; set; } = "Approved";

    [Required]
    public string Feedback { get; set; } = string.Empty;
}

public class CreateAssessmentViewModel
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Type")]
    public string AssessmentType { get; set; } = "C#";

    [Display(Name = "Language")]
    public HerProof.Domain.Assessments.AssessmentLanguage Language { get; set; } = HerProof.Domain.Assessments.AssessmentLanguage.CSharp;

    [Range(5, 480)]
    [Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 90;

    [Display(Name = "Include two ready-made example questions to start from")]
    public bool IncludeExampleQuestions { get; set; }
}

public class AddAssessmentCompetencyViewModel
{
    [Required]
    [Display(Name = "Competency")]
    public Guid CompetencyId { get; set; }

    [Range(0.1, 10)]
    public decimal Weight { get; set; } = 1m;
}

/// <summary>A recruiter/partner/assessor writing their own question - the task the candidate reads
/// and the starting code she edits, typed or pasted in directly rather than limited to the canned
/// C# template auto-applied on Create.</summary>
public class AddAssessmentQuestionViewModel
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Prompt (what the candidate is asked to do)")]
    public string Prompt { get; set; } = string.Empty;

    [Display(Name = "Starter code (what the candidate sees pre-filled)")]
    [StringLength(200_000, ErrorMessage = "Starter code is too large (max 200,000 characters).")]
    public string StarterCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "File name")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Question type")]
    public HerProof.Domain.Assessments.AssessmentQuestionType QuestionType { get; set; } = HerProof.Domain.Assessments.AssessmentQuestionType.Implement;

    [Display(Name = "Hidden test (never shown to the candidate - this is what actually grades her submission)")]
    [StringLength(200_000, ErrorMessage = "Hidden test is too large (max 200,000 characters).")]
    public string? HiddenTestCode { get; set; }
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
