using HerProof.Application.Repositories;
using Xunit;

namespace HerProof.Application.Tests;

public class GitHubRepositoryUrlParserTests
{
    [Theory]
    [InlineData("https://github.com/anko/Order-Service", "anko", "Order-Service")]
    [InlineData("http://github.com/anko/order-service", "anko", "order-service")]
    [InlineData("github.com/anko/order-service", "anko", "order-service")]
    [InlineData("https://github.com/anko/order-service.git", "anko", "order-service")]
    [InlineData("https://github.com/anko/order-service/", "anko", "order-service")]
    [InlineData("https://www.github.com/anko/order-service", "anko", "order-service")]
    [InlineData("https://github.com/anko/order-service/tree/main", "anko", "order-service")]
    public void Parses_well_formed_github_urls(string url, string expectedOwner, string expectedRepo)
    {
        var result = GitHubRepositoryUrlParser.TryParse(url);

        Assert.NotNull(result);
        Assert.Equal(expectedOwner, result.Value.Owner);
        Assert.Equal(expectedRepo, result.Value.Repo);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://gitlab.com/anko/order-service")]
    [InlineData("https://github.com/anko")]
    [InlineData("")]
    public void Rejects_anything_that_is_not_a_github_repo_url(string url)
    {
        Assert.Null(GitHubRepositoryUrlParser.TryParse(url));
    }
}
