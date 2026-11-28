namespace HerProof.Web.Models.Interviews;

/// <summary>Everything the public certificate-verify page is allowed to show. Deliberately excludes
/// transcripts, scores, verdicts and repository details - it only confirms that an AI-assisted
/// interview took place, who it was with, and which competencies it covered (CLAUDE.md rule 9).</summary>
public class PublicInterviewCertificateViewModel
{
    public required string CandidateName { get; init; }
    public required string InterviewerPersona { get; init; }
    public required string Mode { get; init; }
    public required DateTime? CompletedAtUtc { get; init; }
    public required IReadOnlyList<string> CompetencyNames { get; init; }
}
