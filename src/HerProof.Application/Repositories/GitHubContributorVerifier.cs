using System.Text.RegularExpressions;
using HerProof.Domain.Repositories;

namespace HerProof.Application.Repositories;

/// <summary>Checks that a candidate submitting a repository is actually a real contributor to it —
/// not a stranger's public repo submitted as her own. Flags, never blocks (CLAUDE.md rule 5's
/// "transparent, never punitive" principle applied here too): GitHub's contributor list can miss a
/// real contributor (private commit email, squashed PRs), so this is evidence for the assessor to
/// see, not a gate that silently rejects a genuine candidate.</summary>
public static partial class GitHubContributorVerifier
{
    public static string? ExtractUsername(string? gitHubProfileUrl)
    {
        if (string.IsNullOrWhiteSpace(gitHubProfileUrl))
        {
            return null;
        }

        var match = GitHubUsernamePattern().Match(gitHubProfileUrl.Trim());
        return match.Success ? match.Groups["username"].Value : null;
    }

    public static ContributorVerificationStatus Verify(string? candidateGitHubUsername, IReadOnlyCollection<string> contributorLogins)
    {
        if (string.IsNullOrWhiteSpace(candidateGitHubUsername))
        {
            return ContributorVerificationStatus.NotChecked;
        }

        return contributorLogins.Any(login => string.Equals(login, candidateGitHubUsername, StringComparison.OrdinalIgnoreCase))
            ? ContributorVerificationStatus.Confirmed
            : ContributorVerificationStatus.NotConfirmed;
    }

    [GeneratedRegex(@"github\.com/(?<username>[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubUsernamePattern();
}
