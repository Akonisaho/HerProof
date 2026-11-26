using System.Text.RegularExpressions;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Verification;
using HerProof.Infrastructure.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>HP-033, closed: a decision submitted for a competency the candidate never claimed (no
/// CandidateCompetency row yet) used to create the VerificationRecord (correct, append-only) but
/// silently drop the decision on the floor via `candidateCompetency?.ApplyAssessorDecision(...)` —
/// nothing existed to update. The row is now created if missing.</summary>
[Collection(WebAppFactoryCollection.Name)]
public class AssessorDashboardControllerTests
{
    private readonly HerProofWebApplicationFactory _factory;

    public AssessorDashboardControllerTests(HerProofWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Decide_creates_the_candidate_competency_row_when_none_exists()
    {
        Guid candidateId;
        Guid competencyId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
            var candidateUserId = Guid.NewGuid();
            db.Users.Add(new ApplicationUser { Id = candidateUserId, UserName = $"{candidateUserId}@test.local", Email = $"{candidateUserId}@test.local", FullName = "Test Candidate" });
            var candidateProfile = new CandidateProfile(candidateUserId);
            db.CandidateProfiles.Add(candidateProfile);
            var competency = new Competency("Test competency", CompetencyCategory.Software, "v1");
            db.Competencies.Add(competency);
            db.SaveChanges();
            candidateId = candidateProfile.Id;
            competencyId = competency.Id;

            Assert.False(db.CandidateCompetencies.Any(cc => cc.CandidateId == candidateId && cc.CompetencyId == competencyId));
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Assessor));

        var evidencePackResponse = await client.GetAsync($"/assessor/candidates/{candidateId}/competencies/{competencyId}");
        var evidencePackHtml = await evidencePackResponse.Content.ReadAsStringAsync();
        Assert.True(evidencePackResponse.IsSuccessStatusCode, evidencePackHtml);
        var token = Regex.Match(evidencePackHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

        var form = new Dictionary<string, string>
        {
            ["Decision"] = nameof(VerificationDecision.Verified),
            ["FinalLevel"] = "2",
            ["Feedback"] = "Solid work.",
            ["__RequestVerificationToken"] = token,
        };

        var response = await client.PostAsync(
            $"/assessor/candidates/{candidateId}/competencies/{competencyId}/decide",
            new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var candidateCompetency = verifyDb.CandidateCompetencies.Single(cc => cc.CandidateId == candidateId && cc.CompetencyId == competencyId);
        Assert.Equal(CandidateCompetencyStatus.Verified, candidateCompetency.Status);
        Assert.Equal(2, candidateCompetency.VerifiedLevel);
    }
}
