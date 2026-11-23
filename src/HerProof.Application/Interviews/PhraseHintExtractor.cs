using System.Text.RegularExpressions;

namespace HerProof.Application.Interviews;

/// <summary>docs/05 P11: "phrase hints from repo identifiers" help speech-to-text correctly transcribe
/// a candidate naming her own classes/methods out loud, which a general-purpose language model would
/// otherwise mis-hear. Pulls identifier-shaped tokens out of text the engine already has — the
/// question's own code snippet reference and the evidence strings behind it — rather than
/// re-fetching or inventing anything.</summary>
public static partial class PhraseHintExtractor
{
    private const int MaxHints = 20;

    public static IReadOnlyList<string> Extract(params string?[] sourceTexts)
    {
        var hints = new List<string>();
        foreach (var text in sourceTexts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in IdentifierRegex().Matches(text))
            {
                var token = match.Value;
                if (token.Length >= 3 && !hints.Contains(token, StringComparer.Ordinal))
                {
                    hints.Add(token);
                }
            }
        }

        return hints.Take(MaxHints).ToList();
    }

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]{2,}")]
    private static partial Regex IdentifierRegex();
}
