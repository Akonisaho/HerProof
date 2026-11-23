using System.Net;
using System.Net.Http.Json;
using HerProof.Application.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace HerProof.Infrastructure.Repositories;

/// <summary>Public repos only for now (docs/05 Phase 2 scope) — no auth token is sent, so this works
/// unauthenticated against GitHub's REST API. Private repo access via GitHub App/OAuth is
/// STUB(HP-011), deferred until there's a real client id/secret to test against.
/// Caches SHA resolution briefly and tree/file content by commit SHA indefinitely (pinned commits are
/// immutable, so this is always safe — docs/01 section 15: "queue retry with backoff... caching").</summary>
public class GitHubClient(HttpClient httpClient, IMemoryCache cache) : IGitHubClient
{
    public async Task<string> ResolveBranchToCommitShaAsync(string owner, string repo, string branch, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:sha:{owner}/{repo}@{branch}";
        if (cache.TryGetValue(cacheKey, out string? cachedSha) && cachedSha is not null)
        {
            return cachedSha;
        }

        using var response = await httpClient.GetAsync($"repos/{owner}/{repo}/commits/{branch}", cancellationToken);
        EnsureSuccess(response, owner, repo);

        var payload = await response.Content.ReadFromJsonAsync<CommitResponse>(cancellationToken: cancellationToken);
        var sha = payload?.Sha ?? throw new InvalidOperationException("GitHub returned no commit SHA.");

        cache.Set(cacheKey, sha, TimeSpan.FromMinutes(5));
        return sha;
    }

    public async Task<IReadOnlyList<GitHubTreeEntry>> GetTreeAsync(string owner, string repo, string commitSha, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:tree:{owner}/{repo}@{commitSha}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<GitHubTreeEntry>? cachedTree) && cachedTree is not null)
        {
            return cachedTree;
        }

        using var response = await httpClient.GetAsync($"repos/{owner}/{repo}/git/trees/{commitSha}?recursive=1", cancellationToken);
        EnsureSuccess(response, owner, repo);

        var payload = await response.Content.ReadFromJsonAsync<TreeResponse>(cancellationToken: cancellationToken);
        var entries = (payload?.Tree ?? [])
            .Select(e => new GitHubTreeEntry(e.Path, e.Type, e.Size))
            .ToList();

        // Pinned by commit SHA: content at this SHA can never change (ADR-06), safe to cache for the process lifetime.
        cache.Set(cacheKey, (IReadOnlyList<GitHubTreeEntry>)entries, TimeSpan.FromHours(12));
        return entries;
    }

    public async Task<string> GetFileContentAsync(string owner, string repo, string commitSha, string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:file:{owner}/{repo}@{commitSha}:{path}";
        if (cache.TryGetValue(cacheKey, out string? cachedContent) && cachedContent is not null)
        {
            return cachedContent;
        }

        using var response = await httpClient.GetAsync($"repos/{owner}/{repo}/contents/{path}?ref={commitSha}", cancellationToken);
        EnsureSuccess(response, owner, repo);

        var payload = await response.Content.ReadFromJsonAsync<ContentResponse>(cancellationToken: cancellationToken);
        if (payload?.Content is null || payload.Encoding != "base64")
        {
            return string.Empty;
        }

        var content = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload.Content.Replace("\n", string.Empty)));
        cache.Set(cacheKey, content, TimeSpan.FromHours(12));
        return content;
    }

    public async Task<IReadOnlyList<string>> GetContributorLoginsAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"github:contributors:{owner}/{repo}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        using var response = await httpClient.GetAsync($"repos/{owner}/{repo}/contributors?per_page=100", cancellationToken);
        EnsureSuccess(response, owner, repo);

        var payload = await response.Content.ReadFromJsonAsync<List<ContributorResponse>>(cancellationToken: cancellationToken);
        var logins = (payload ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Login))
            .Select(c => c.Login!)
            .ToList();

        cache.Set(cacheKey, (IReadOnlyList<string>)logins, TimeSpan.FromHours(1));
        return logins;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string owner, string repo)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GitHubNotFoundException(owner, repo);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden
            && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
            && remaining.FirstOrDefault() == "0")
        {
            var retryAfter = TimeSpan.FromSeconds(60);
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)
                && long.TryParse(resetValues.FirstOrDefault(), out var resetEpochSeconds))
            {
                var resetAt = DateTimeOffset.FromUnixTimeSeconds(resetEpochSeconds);
                retryAfter = resetAt - DateTimeOffset.UtcNow;
            }

            throw new GitHubRateLimitException(retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(60));
        }

        response.EnsureSuccessStatusCode();
    }

    private record CommitResponse(string Sha);

    private record TreeResponse(List<TreeEntryResponse> Tree);

    private record TreeEntryResponse(string Path, string Type, long? Size);

    private record ContentResponse(string? Content, string? Encoding);

    private record ContributorResponse(string? Login);
}
