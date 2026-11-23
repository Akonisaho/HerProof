using HerProof.Application.Interviews;
using Xunit;

namespace HerProof.Application.Tests;

public class ForbiddenContentFilterTests
{
    [Theory]
    [InlineData("Her accent made it hard to follow.")]
    [InlineData("The candidate seemed nervous.")]
    [InlineData("I noticed some hesitation and pauses.")]
    [InlineData("This answer sounds rehearsed.")]
    [InlineData("She code-switched between English and isiZulu.")]
    [InlineData("Not very confident in her answer.")]
    public void Rejects_forbidden_biometric_and_demographic_language(string text)
    {
        Assert.True(ForbiddenContentFilter.ContainsForbiddenContent(text));
    }

    [Theory]
    [InlineData("The answer correctly explains the service layer.")]
    [InlineData("OrderService.cs line 30 shows a database write, which contradicts the claim of caching.")]
    [InlineData("Walk me through what this method does.")]
    public void Allows_ordinary_technical_evaluation_language(string text)
    {
        Assert.False(ForbiddenContentFilter.ContainsForbiddenContent(text));
    }
}
