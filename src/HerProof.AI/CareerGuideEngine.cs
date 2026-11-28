using System.Text.Json;
using System.Text.Json.Serialization;
using HerProof.Application.AI;
using Microsoft.Extensions.Logging;

namespace HerProof.AI;

public record CareerGuideReply(string Reply, string? SuggestedGoal);

/// <summary>A friendly chat for a candidate who has no idea what to aim for yet - same discipline as
/// NolwaziInterviewEngine: a system prompt, IAIService.CompleteJsonAsync (JSON-only, never a tool-use
/// surface), strict DTO deserialization, retry-once-then-fail-safe. The candidate's typed message is
/// untrusted input wrapped as data, never as instructions (CLAUDE.md rule 7). This never recommends,
/// scores, or verifies anything - it only ever suggests one of the real, already-built roadmap goals,
/// and that suggestion is whitelist-checked against the real goal list before it's ever trusted.</summary>
public class CareerGuideEngine(IAIService aiService, IReadOnlyCollection<string> knownGoals, ILogger<CareerGuideEngine>? logger = null)
{
    public const string PromptVersion = "career-guide-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CareerGuideReply> ReplyAsync(string conversationSoFar, string candidateMessage, CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt();
        var userContent = $"Conversation so far:\n{conversationSoFar}\n\nHer new message:\n{candidateMessage}";

        var dto = await TryCompleteAsync(systemPrompt, userContent, cancellationToken)
            ?? await TryCompleteAsync(systemPrompt, userContent, cancellationToken);

        if (dto is null || string.IsNullOrWhiteSpace(dto.Reply))
        {
            logger?.LogWarning("Career guide: model response could not be parsed (both attempts failed).");
            return new CareerGuideReply(
                "I'm having trouble replying right now - try the search box above instead, or ask me again in a moment.",
                null);
        }

        // Never trust a goal name the model invented - only ever offer one that's a real, resolvable
        // roadmap (same discipline as SnippetReferenceVerifier in Nolwazi's engine: verify before using).
        var suggestedGoal = dto.SuggestedGoal is not null && knownGoals.Contains(dto.SuggestedGoal, StringComparer.OrdinalIgnoreCase)
            ? knownGoals.First(g => string.Equals(g, dto.SuggestedGoal, StringComparison.OrdinalIgnoreCase))
            : null;

        return new CareerGuideReply(dto.Reply, suggestedGoal);
    }

    private string BuildSystemPrompt() =>
        $$"""
        You are a warm, encouraging career guide inside HerProof, a skills-verification platform for
        women in South Africa. You talk to candidates who do not yet know what career path to aim for.

        Known roadmap goals you may suggest (and only these, spelled exactly): {{string.Join(", ", knownGoals)}}

        Rules:
        - Be brief, warm, and practical - two or three sentences at most.
        - Never discuss anything except career direction and what skill to build next.
        - Never mention accent, appearance, fluency, confidence, or any personal trait - only skills and goals.
        - Several goals above are the same role in different programming languages (for example
          "Software Developer (C#/.NET)" and "Software Developer (Java)"). If she says something
          generic like "software developer", "I want to code", or "build apps" without naming a
          language, do NOT guess one - set suggestedGoal to null and ask which language she wants to
          learn or already knows, naming the real options from the list above. Only set suggestedGoal
          once she's actually told you the language (or it's otherwise unambiguous, e.g. she already
          named "Java" or "C#" herself).
        - If you can confidently match her message to one of the known goals above, set suggestedGoal to
          that exact string. Otherwise set suggestedGoal to null and ask one short clarifying question.
        - Respond with ONLY a JSON object, no other text: {"reply": "...", "suggestedGoal": "..." or null}
        """;

    private async Task<CareerGuideDto?> TryCompleteAsync(string systemPrompt, string userContent, CancellationToken cancellationToken)
    {
        try
        {
            var completion = await aiService.CompleteJsonAsync(systemPrompt, userContent, PromptVersion, cancellationToken);
            return JsonSerializer.Deserialize<CareerGuideDto>(completion.RawJson, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException)
        {
            logger?.LogWarning(ex, "Career guide: completion attempt failed.");
            return null;
        }
    }

    private record CareerGuideDto([property: JsonPropertyName("reply")] string Reply, [property: JsonPropertyName("suggestedGoal")] string? SuggestedGoal);
}
