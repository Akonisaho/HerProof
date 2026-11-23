using System.Net.Http.Json;
using HerProof.Application.Assessments;
using HerProof.Domain.Assessments;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Controllers.Api;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>HP-026, closed: the Electron agent's timer is now server-authoritative — the server, not
/// the client, decides whether a code submission is on-time or a timeout, based on its own
/// StartedAtUtc and the assessment's DurationMinutes.</summary>
[Collection(WebAppFactoryCollection.Name)]
public class AssessmentSessionInfoControllerTests
{
    private readonly HerProofWebApplicationFactory _factory;

    public AssessmentSessionInfoControllerTests(HerProofWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private (Guid sessionId, string token) SeedSession(DateTime startedAtUtc, int durationMinutes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ISessionEventTokenService>();

        var assessment = new Assessment("Practical test", "desc", "Coding", durationMinutes, Guid.NewGuid());
        db.Assessments.Add(assessment);

        var session = new AssessmentSession(assessment.Id, Guid.NewGuid());
        session.GiveConsent(startedAtUtc);
        session.Start(startedAtUtc);
        db.AssessmentSessions.Add(session);
        db.SaveChanges();

        var token = tokenService.Issue(session.Id, DateTime.UtcNow);
        return (session.Id, token);
    }

    [Fact]
    public async Task GetInfo_without_a_token_is_unauthorized()
    {
        var (sessionId, _) = SeedSession(DateTime.UtcNow, 60);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/assessment-sessions/{sessionId}/info");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetInfo_with_a_valid_token_returns_the_real_start_time_and_duration()
    {
        var startedAt = DateTime.UtcNow.AddMinutes(-5);
        var (sessionId, token) = SeedSession(startedAt, 60);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.GetAsync($"/api/assessment-sessions/{sessionId}/info");
        response.EnsureSuccessStatusCode();
        var info = await response.Content.ReadFromJsonAsync<SessionInfoResponse>();

        Assert.NotNull(info);
        Assert.Equal(60, info!.DurationMinutes);
        Assert.NotNull(info.StartedAtUtc);
        Assert.Equal(startedAt, info.StartedAtUtc!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SubmitCode_before_the_deadline_is_a_real_submission()
    {
        var (sessionId, token) = SeedSession(DateTime.UtcNow, 60);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.PostAsJsonAsync($"/api/assessment-sessions/{sessionId}/code", new SubmitCodeRequest("// solution"));
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var session = db.AssessmentSessions.Single(s => s.Id == sessionId);
        Assert.Equal(AssessmentSessionStatus.Submitted, session.Status);
    }

    [Fact]
    public async Task SubmitCode_past_the_deadline_is_a_timeout_not_a_submission()
    {
        var (sessionId, token) = SeedSession(DateTime.UtcNow.AddMinutes(-90), 60);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var response = await client.PostAsJsonAsync($"/api/assessment-sessions/{sessionId}/code", new SubmitCodeRequest("// solution"));
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var session = db.AssessmentSessions.Single(s => s.Id == sessionId);
        Assert.Equal(AssessmentSessionStatus.TimedOut, session.Status);
    }
}
