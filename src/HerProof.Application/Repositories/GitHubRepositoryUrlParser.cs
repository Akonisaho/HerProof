using System.Text.RegularExpressions;

namespace HerProof.Application.Repositories;

public static partial class GitHubRepositoryUrlParser
{
    /// <summary>Accepts github.com/owner/repo, with or without https://, a trailing .git, trailing
    /// slash, or a /tree/branch suffix (branch is submitted separately, so that suffix is ignored).</summary>
    public static (string Owner, string Repo)? TryParse(string url)
    {
        var match = UrlPattern().Match(url.Trim());
        if (!match.Success)
        {
            return null;
        }

        return (match.Groups["owner"].Value, match.Groups["repo"].Value);
    }

    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?github\.com/(?<owner>[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?(?:/.*)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
