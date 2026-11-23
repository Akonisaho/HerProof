using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>The authorisation test matrix CLAUDE.md P03 asks for, run against
/// CandidateProfileController.Index: anonymous, wrong role, right role wrong owner, right role
/// right owner. Uses TestAuthHandler instead of a real cookie login (see its header contract).</summary>
[Collection(WebAppFactoryCollection.Name)]
public class AuthorizationMatrixTests
{
    private readonly HerProofWebApplicationFactory _factory;
    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _ownerProfileId;

    public AuthorizationMatrixTests(HerProofWebApplicationFactory factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var ownerProfile = new CandidateProfile(_ownerUserId);
        var otherProfile = new CandidateProfile(_otherUserId);
        db.CandidateProfiles.AddRange(ownerProfile, otherProfile);
        db.SaveChanges();
        _ownerProfileId = ownerProfile.Id;
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/candidates/{_ownerProfileId}/profile");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_role_is_forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _ownerUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Recruiter));

        var response = await client.GetAsync($"/candidates/{_ownerProfileId}/profile");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Right_role_wrong_owner_is_forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _otherUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));

        var response = await client.GetAsync($"/candidates/{_ownerProfileId}/profile");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Right_role_right_owner_is_ok()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _ownerUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));

        var response = await client.GetAsync($"/candidates/{_ownerProfileId}/profile");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
