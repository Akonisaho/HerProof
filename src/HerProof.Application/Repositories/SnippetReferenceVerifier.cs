namespace HerProof.Application.Repositories;

/// <summary>Shared by repository findings (P08) and interview questions/turns (P10): both must cite a
/// file and line range that actually exists in the fetched, pinned-commit content (docs/01 section 6,
/// docs/06 section 5/7). One parser, one rule, used everywhere a snippet reference needs checking.</summary>
public static class SnippetReferenceVerifier
{
    public static bool Exists(FetchedRepositoryContent content, string filePath, string? lineReference)
    {
        var file = content.Files.FirstOrDefault(f => f.Path == filePath);
        if (file is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(lineReference))
        {
            return true;
        }

        if (!TryParseLineRange(lineReference, out var start, out var end))
        {
            return false;
        }

        var lineCount = file.Content.Split('\n').Length;
        return start >= 1 && end >= start && end <= lineCount;
    }

    /// <summary>Accepts "42" or "42-57".</summary>
    public static bool TryParseLineRange(string lineReference, out int start, out int end)
    {
        start = end = 0;
        var parts = lineReference.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
        {
            if (!int.TryParse(parts[0], out start))
            {
                return false;
            }

            end = start;
            return true;
        }

        return parts.Length == 2 && int.TryParse(parts[0], out start) && int.TryParse(parts[1], out end);
    }
}
