namespace HerProof.Application.Interviews;

/// <summary>CLAUDE.md rule 4 / docs/06 section 6: never infer or mention accent, pronunciation,
/// fluency, pauses, appearance, emotion, confidence, or "sounds rehearsed". This is the last line of
/// defence — applied to every piece of free text an LLM produces before it can reach a candidate or
/// be stored as evaluation evidence, regardless of how well the prompt asked it not to.</summary>
public static class ForbiddenContentFilter
{
    private static readonly string[] ForbiddenTerms =
    [
        "accent", "pronunciation", "pronounce", "fluency", "fluent", "pause", "paused",
        "code-switch", "code switching", "appearance", "looks like", "attractive",
        "emotion", "nervous", "confidence", "confident", "tone of voice", "voice stress",
        "eye contact", "sounds rehearsed", "rehearsed",
    ];

    public static bool ContainsForbiddenContent(string text) =>
        ForbiddenTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
