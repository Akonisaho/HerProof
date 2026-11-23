namespace HerProof.Application.Repositories;

public record GitHubTreeEntry(string Path, string Type, long? SizeBytes);

/// <summary>Read-only access to public (and, later, authorised private) repositories (docs/03
/// section 4: minimum scopes, read-only). Implementations must handle GitHub's rate limits with
/// backoff and caching (docs/01 section 6) — callers should not need to think about either.</summary>
public interface IGitHubClient
{
    Task<string> ResolveBranchToCommitShaAsync(string owner, string repo, string branch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubTreeEntry>> GetTreeAsync(string owner, string repo, string commitSha, CancellationToken cancellationToken = default);

    Task<string> GetFileContentAsync(string owner, string repo, string commitSha, string path, CancellationToken cancellationToken = default);

    /// <summary>Real GitHub contributor logins for this repo (docs/05: verify the submitting
    /// candidate actually contributed to the code she's claiming, not a stranger's public repo).</summary>
    Task<IReadOnlyList<string>> GetContributorLoginsAsync(string owner, string repo, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when GitHub's rate limit is hit. Callers should surface RetryAfter to the
/// candidate rather than silently fail (docs/01 section 15: "queue retry with backoff").</summary>
public class GitHubRateLimitException(TimeSpan retryAfter) : Exception($"GitHub API rate limit reached. Try again in {retryAfter.TotalSeconds:F0}s.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

public class GitHubNotFoundException(string owner, string repo) : Exception($"Repository {owner}/{repo} was not found or is not accessible.")
{
}
