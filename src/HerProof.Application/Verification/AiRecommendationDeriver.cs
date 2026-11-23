using HerProof.Domain.Interviews;

namespace HerProof.Application.Verification;

/// <summary>A suggestion for the assessor to look at, never a decision (docs/05 P14: "AI
/// recommendations are visually distinct from human decisions"). This function has no way to
/// produce a VerificationRecord — it returns a label the UI renders in a clearly different box,
/// nothing more.</summary>
public enum AiSuggestion
{
    InsufficientEvidence,
    NotDemonstrated,
    Developing,
    Verified,
}

public static class AiRecommendationDeriver
{
    public static AiSuggestion Derive(IReadOnlyList<Verdict> interviewVerdicts, bool hasRepositoryFindings)
    {
        if (interviewVerdicts.Count == 0 && !hasRepositoryFindings)
        {
            return AiSuggestion.InsufficientEvidence;
        }

        if (interviewVerdicts.Any(v => v == Verdict.NotDemonstrated))
        {
            return AiSuggestion.NotDemonstrated;
        }

        if (interviewVerdicts.Count > 0 && interviewVerdicts.All(v => v == Verdict.Understood))
        {
            return AiSuggestion.Verified;
        }

        return AiSuggestion.Developing;
    }
}
