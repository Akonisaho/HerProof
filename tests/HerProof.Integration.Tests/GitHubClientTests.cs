using System.Net;
using System.Text;
using HerProof.Application.Repositories;
using HerProof.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>"Integration tests with a fake GitHub server" (docs/05 P07): a stub HttpMessageHandler
/// stands in for api.github.com so these run with no network and no real GitHub rate limit risk.</summary>
public class GitHubClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private static GitHubClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubClient(httpClient, new MemoryCache(new MemoryCacheOptions()));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task Resolves_branch_to_commit_sha()
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, """{"sha":"abc123def456"}"""));

        var sha = await client.ResolveBranchToCommitShaAsync("anko", "order-service", "main");

        Assert.Equal("abc123def456", sha);
    }

    [Fact]
    public async Task Sha_resolution_is_cached_and_does_not_call_the_server_twice()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            return JsonResponse(HttpStatusCode.OK, """{"sha":"abc123"}""");
        });

        await client.ResolveBranchToCommitShaAsync("anko", "order-service", "main");
        await client.ResolveBranchToCommitShaAsync("anko", "order-service", "main");

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Not_found_repository_throws_github_not_found_exception()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<GitHubNotFoundException>(() => client.ResolveBranchToCommitShaAsync("anko", "missing-repo", "main"));
    }

    [Fact]
    public async Task Rate_limit_response_throws_with_a_retry_after_duration()
    {
        var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(42).ToUnixTimeSeconds().ToString());
            return response;
        });

        var ex = await Assert.ThrowsAsync<GitHubRateLimitException>(() => client.ResolveBranchToCommitShaAsync("anko", "order-service", "main"));

        Assert.InRange(ex.RetryAfter.TotalSeconds, 30, 50);
    }

    [Fact]
    public async Task A_403_that_is_not_rate_limiting_is_not_swallowed_as_rate_limit()
    {
        // E.g. a private repo with no access: 403 but X-RateLimit-Remaining is absent or non-zero.
        // HP-019, closed: this used to surface as an unhandled HttpRequestException (an unfriendly
        // 500 to the candidate); it's now a GitHubApiException the controllers catch and turn into
        // a plain-language message.
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var ex = await Assert.ThrowsAsync<GitHubApiException>(() => client.ResolveBranchToCommitShaAsync("anko", "private-repo", "main"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task Gets_recursive_tree_and_caches_it_by_commit_sha()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            return JsonResponse(HttpStatusCode.OK, """{"tree":[{"path":"src/Program.cs","type":"blob","size":123},{"path":"src","type":"tree"}]}""");
        });

        var tree1 = await client.GetTreeAsync("anko", "order-service", "abc123");
        var tree2 = await client.GetTreeAsync("anko", "order-service", "abc123");

        Assert.Equal(1, callCount);
        Assert.Equal(2, tree1.Count);
        Assert.Contains(tree1, e => e.Path == "src/Program.cs" && e.Type == "blob" && e.SizeBytes == 123);
    }

    [Fact]
    public async Task Gets_and_decodes_base64_file_content()
    {
        var expected = "public class Program {}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(expected));
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, $$"""{"content":"{{encoded}}","encoding":"base64"}"""));

        var content = await client.GetFileContentAsync("anko", "order-service", "abc123", "src/Program.cs");

        Assert.Equal(expected, content);
    }

    [Fact]
    public async Task Full_fetch_pipeline_skips_vendored_and_binary_files_via_the_real_selector()
    {
        var treeJson = """
            {"tree":[
                {"path":"src/Program.cs","type":"blob","size":50},
                {"path":"node_modules/pkg/index.js","type":"blob","size":50},
                {"path":"bin/Debug/App.dll","type":"blob","size":50}
            ]}
            """;
        var fileEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("// content"));

        var client = CreateClient(request => request.RequestUri!.AbsolutePath.Contains("/git/trees/")
            ? JsonResponse(HttpStatusCode.OK, treeJson)
            : JsonResponse(HttpStatusCode.OK, $$"""{"content":"{{fileEncoded}}","encoding":"base64"}"""));

        var fetcher = new RepositoryContentFetcher(client);
        var result = await fetcher.FetchAsync("anko", "order-service", "abc123");

        Assert.Single(result.Files);
        Assert.Equal("src/Program.cs", result.Files[0].Path);
    }
}
