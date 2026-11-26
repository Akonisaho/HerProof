using System.Text.RegularExpressions;

namespace HerProof.Application.AI;

/// <summary>LLMs routinely return JSON keys in snake_case (e.g. "competency_name") even when the
/// system prompt specifies camelCase and the API enforces JSON-object mode — this is a well-known
/// model quirk, not a provider bug, and happens with both real providers used here (Groq, Anthropic).
/// Every DTO in this codebase expects camelCase/PascalCase, so a model that reverts to snake_case
/// silently deserializes every affected field to null/default rather than throwing — which is exactly
/// why "Thandi couldn't generate questions" and "AI analysis could not be parsed" kept recurring even
/// when the provider call itself succeeded. Rewriting only key positions (immediately followed by
/// '":') rather than the whole JSON text means a string VALUE containing an underscore — a file path,
/// a snippet of code — is never touched.</summary>
public static partial class LlmJsonKeyNormalizer
{
    public static string ToCamelCaseKeys(string json) =>
        SnakeCaseKeyPattern().Replace(json, match =>
        {
            var key = match.Groups["key"].Value;
            var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
            var camel = parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
            return $"\"{camel}\":";
        });

    // A key position is a quoted identifier-like token immediately followed by ':' — values are
    // always followed by ',', '}' or ']', never ':', so this never matches inside a string value.
    [GeneratedRegex("\"(?<key>[a-z][a-z0-9]*(?:_[a-z0-9]+)+)\"\\s*:")]
    private static partial Regex SnakeCaseKeyPattern();
}
