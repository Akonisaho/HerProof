namespace HerProof.Application.Repositories;

/// <summary>Pure selection logic (docs/05 P07): which tree entries to fetch for analysis, given size
/// and count caps, skipping binaries and vendored directories. No I/O — callers fetch the content of
/// whatever this returns.</summary>
public static class RepositoryContentSelector
{
    public const int MaxFiles = 200;
    public const long MaxTotalBytes = 2 * 1024 * 1024; // 2 MB, generous for a hackathon-scale repo.

    private static readonly string[] VendoredPathSegments =
    [
        "/node_modules/", "/bin/", "/obj/", "/.git/", "/dist/", "/vendor/", "/packages/", "/.vs/", "/out/",
    ];

    private static readonly string[] AllowedExtensions =
    [
        ".cs", ".csproj", ".sln", ".json", ".md", ".ts", ".tsx", ".js", ".jsx", ".yml", ".yaml",
        ".config", ".razor", ".cshtml", ".py", ".txt", ".editorconfig",
    ];

    public static IReadOnlyList<GitHubTreeEntry> Select(IReadOnlyList<GitHubTreeEntry> tree)
    {
        var candidates = tree
            .Where(e => e.Type == "blob")
            .Where(e => !IsVendored(e.Path))
            .Where(e => IsAllowedExtension(e.Path) || IsReadme(e.Path))
            .OrderByDescending(e => IsReadme(e.Path)) // README first: cheap, high-value context for the analyser.
            .ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = new List<GitHubTreeEntry>();
        long totalBytes = 0;

        foreach (var entry in candidates)
        {
            if (selected.Count >= MaxFiles)
            {
                break;
            }

            var size = entry.SizeBytes ?? 0;
            if (totalBytes + size > MaxTotalBytes)
            {
                continue;
            }

            selected.Add(entry);
            totalBytes += size;
        }

        return selected;
    }

    private static bool IsVendored(string path)
    {
        var normalized = "/" + path.Replace('\\', '/') + "/";
        return VendoredPathSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllowedExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsReadme(string path) =>
        Path.GetFileNameWithoutExtension(path).Equals("README", StringComparison.OrdinalIgnoreCase);
}
