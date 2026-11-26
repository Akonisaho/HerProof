using System.ComponentModel.DataAnnotations;

namespace HerProof.Web.Models.Interviews;

public class SubmitAnswerViewModel
{
    public Guid TurnId { get; set; }

    [Required]
    [Display(Name = "Your answer")]
    public string Answer { get; set; } = string.Empty;
}

/// <summary>docs/05 P11's consent screen: shown once before a voice interview starts. Declining still
/// starts the interview — text-first, same rubric, no penalty for switching (CLAUDE.md rule 5's
/// "transparent, never punitive" principle applied here too).</summary>
public class VoiceConsentViewModel
{
    public Guid RepositoryId { get; set; }
    public Guid? OpportunityId { get; set; }
    public bool ConsentGiven { get; set; }
}
