using HerProof.Application.Repositories;
using HerProof.Domain.Repositories;
using Xunit;

namespace HerProof.Application.Tests;

public class GitHubContributorVerifierTests
{
    [Theory]
    [InlineData("https://github.com/anko-dev", "anko-dev")]
    [InlineData("https://github.com/anko-dev/", "anko-dev")]
    [InlineData("http://github.com/Anko-Dev", "Anko-Dev")]
    [InlineData("github.com/anko-dev", "anko-dev")]
    public void ExtractUsername_parses_common_profile_url_shapes(string url, string expected)
    {
        Assert.Equal(expected, GitHubContributorVerifier.ExtractUsername(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url at all")]
    [InlineData("https://linkedin.com/in/anko-dev")]
    public void ExtractUsername_returns_null_for_anything_that_is_not_a_github_profile(string? url)
    {
        Assert.Null(GitHubContributorVerifier.ExtractUsername(url));
    }

    [Fact]
    public void Verify_is_not_checked_when_candidate_has_no_declared_username()
    {
        var result = GitHubContributorVerifier.Verify(null, ["octocat"]);

        Assert.Equal(ContributorVerificationStatus.NotChecked, result);
    }

    [Fact]
    public void Verify_confirms_a_case_insensitive_match()
    {
        var result = GitHubContributorVerifier.Verify("Octocat", ["octocat", "other-dev"]);

        Assert.Equal(ContributorVerificationStatus.Confirmed, result);
    }

    [Fact]
    public void Verify_flags_when_the_candidate_is_not_in_the_contributor_list()
    {
        var result = GitHubContributorVerifier.Verify("someone-else", ["octocat", "other-dev"]);

        Assert.Equal(ContributorVerificationStatus.NotConfirmed, result);
    }
}
