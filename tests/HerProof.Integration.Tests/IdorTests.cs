using HerProof.Domain.Identity;
using HerProof.Domain.Opportunities;
using HerProof.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>Object-level authorisation (IDOR) coverage for endpoints added in P14-P16 (docs/03
/// section 12), on top of the P03 matrix in AuthorizationMatrixTests.</summary>
[Collection(WebAppFactoryCollection.Name)]
public class IdorTests
{
    private readonly HerProofWebApplicationFactory _factory;

    public IdorTests(HerProofWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Assessor_dashboard_rejects_candidate_role()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));

        var response = await client.GetAsync("/assessor");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assessor_dashboard_rejects_anonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/assessor");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evidence_pack_rejects_candidate_role()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));

        var response = await client.GetAsync($"/assessor/candidates/{Guid.NewGuid()}/competencies/{Guid.NewGuid()}");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organisation_applications_rejects_a_member_of_a_different_organisation()
    {
        var ownerUserId = Guid.NewGuid();
        var otherOrgMemberId = Guid.NewGuid();
        Guid opportunityId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
            var ownedOrganisation = new Organisation("Owned Org", null, null);
            var otherOrganisation = new Organisation("Other Org", null, null);
            db.Organisations.AddRange(ownedOrganisation, otherOrganisation);

            var opportunity = new Opportunity(ownedOrganisation.Id, "Test role", "desc", OpportunityType.Job, null, null);
            db.Opportunities.Add(opportunity);
            opportunityId = opportunity.Id;

            db.OrganisationMembers.Add(new OrganisationMember(ownedOrganisation.Id, ownerUserId, OrganisationRole.Administrator));
            db.OrganisationMembers.Add(new OrganisationMember(otherOrganisation.Id, otherOrgMemberId, OrganisationRole.Administrator));
            await db.SaveChangesAsync();

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, otherOrgMemberId.ToString());
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Recruiter));

            var response = await client.GetAsync($"/organisations/{ownedOrganisation.Id}/opportunities/{opportunityId}/applications");

            Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Organisation_applications_allows_an_actual_member()
    {
        var ownerUserId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var organisation = new Organisation("Owned Org 2", null, null);
        db.Organisations.Add(organisation);

        var opportunity = new Opportunity(organisation.Id, "Test role", "desc", OpportunityType.Job, null, null);
        db.Opportunities.Add(opportunity);

        db.OrganisationMembers.Add(new OrganisationMember(organisation.Id, ownerUserId, OrganisationRole.Administrator));
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, ownerUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Recruiter));

        var response = await client.GetAsync($"/organisations/{organisation.Id}/opportunities/{opportunity.Id}/applications");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_users_list_rejects_a_recruiter()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Recruiter));

        var response = await client.GetAsync("/admin/users");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_users_list_rejects_anonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/users");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_partner_inquiries_rejects_a_recruiter()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Recruiter));

        var response = await client.GetAsync("/admin/partner-inquiries");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_partner_inquiries_rejects_anonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/partner-inquiries");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Partner_form_is_reachable_anonymously()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/partner");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
