using HerProof.Application.Verification;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Passports;
using HerProof.Domain.Verification;
using HerProof.Infrastructure.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>Reproduces "export PDF throws an error" by exercising the real endpoint end to end
/// (not just unit-testing the exporter) - a candidate with at least one verified credential, asking
/// for /passport/export.pdf, exactly as the browser would.</summary>
[Collection(WebAppFactoryCollection.Name)]
public class PassportControllerExportTests
{
    private readonly HerProofWebApplicationFactory _factory;

    public PassportControllerExportTests(HerProofWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExportPdf_for_a_candidate_with_a_verified_credential_returns_a_pdf()
    {
        var userId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();

            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = "export-test@herproof.test",
                NormalizedUserName = "EXPORT-TEST@HERPROOF.TEST",
                Email = "export-test@herproof.test",
                NormalizedEmail = "EXPORT-TEST@HERPROOF.TEST",
                FullName = "Export Test Candidate",
            });

            var candidateProfile = new CandidateProfile(userId);
            db.CandidateProfiles.Add(candidateProfile);

            var competency = new Competency("C#", CompetencyCategory.Software, "v1");
            db.Competencies.Add(competency);

            var record = VerificationDecisionService.Create(
                actingUserIsAssessor: true, candidateProfile.Id, competency.Id, assessmentId: Guid.Empty,
                finalLevel: 2, Guid.NewGuid(), VerificationDecision.Verified, "Demonstrated in a real repository.",
                DateTime.UtcNow, supersedesId: null);
            db.Add(record);

            var passport = new SkillsPassport(candidateProfile.Id, "HP-TEST-0001", DateTime.UtcNow);
            passport.IssueCredential(record.Id, "HP-TEST-0001-CS-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(2));
            db.Add(passport);

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));

        var response = await client.GetAsync("/passport/export.pdf");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}. Body: {body}");
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }
}
