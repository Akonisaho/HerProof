using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HerProof.Domain.Assessments;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>Covers the browser-based practical assessment path added alongside the Electron agent:
/// cookie-authenticated integrity events (not bearer-token) and the server-authoritative deadline
/// check on Submit. The missing `db.Add()` for the new IntegrityEvent (caught here) is the exact
/// same class of bug HP-026's AssessmentSessionInfoController hit — the AppendOnlyInterceptor
/// throws instead of silently losing the write, which is exactly why these tests exist.</summary>
[Collection(WebAppFactoryCollection.Name)]
public class AssessmentSessionsControllerBrowserTests
{
    private readonly HerProofWebApplicationFactory _factory;

    public AssessmentSessionsControllerBrowserTests(HerProofWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<(HttpClient client, Guid sessionId)> SeedInProgressSessionAsync(DateTime startedAtUtc, int durationMinutes)
    {
        var userId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();

        var candidateProfile = new CandidateProfile(userId);
        db.CandidateProfiles.Add(candidateProfile);

        var assessment = new Assessment("Test assessment", "desc", "C#", durationMinutes, Guid.NewGuid());
        db.Assessments.Add(assessment);

        var session = new AssessmentSession(assessment.Id, candidateProfile.Id);
        session.GiveConsent(startedAtUtc);
        session.Start(startedAtUtc);
        db.AssessmentSessions.Add(session);
        db.SaveChanges();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));

        return Task.FromResult((client, session.Id));
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task RecordEvent_persists_a_real_integrity_event()
    {
        var (client, sessionId) = await SeedInProgressSessionAsync(DateTime.UtcNow, 90);
        var token = await ExtractAntiforgeryTokenAsync(client, $"/assessment-sessions/{sessionId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/assessment-sessions/{sessionId}/events")
        {
            Content = JsonContent.Create(new { eventType = "focus_lost" }),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var session = db.AssessmentSessions.Include(s => s.IntegrityEvents).Single(s => s.Id == sessionId);
        Assert.Contains(session.IntegrityEvents, e => e.EventType == "focus_lost");
    }

    [Fact]
    public async Task RecordEvent_rejects_an_event_type_a_browser_tab_cannot_honestly_claim()
    {
        var (client, sessionId) = await SeedInProgressSessionAsync(DateTime.UtcNow, 90);
        var token = await ExtractAntiforgeryTokenAsync(client, $"/assessment-sessions/{sessionId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/assessment-sessions/{sessionId}/events")
        {
            Content = JsonContent.Create(new { eventType = "window_closed" }),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordEvent_rejects_a_different_candidates_session()
    {
        var (_, sessionId) = await SeedInProgressSessionAsync(DateTime.UtcNow, 90);

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        otherClient.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, nameof(UserRole.Candidate));
        var token = await ExtractAntiforgeryTokenAsync(otherClient, "/assessment-sessions/browse");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/assessment-sessions/{sessionId}/events")
        {
            Content = JsonContent.Create(new { eventType = "focus_lost" }),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await otherClient.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Submit_past_the_deadline_is_a_timeout_not_a_submission()
    {
        var (client, sessionId) = await SeedInProgressSessionAsync(DateTime.UtcNow.AddMinutes(-120), 90);
        var token = await ExtractAntiforgeryTokenAsync(client, $"/assessment-sessions/{sessionId}");

        var form = new Dictionary<string, string>
        {
            ["FilesJson"] = """[{"path":"Solution.cs","content":"// late"}]""",
            ["__RequestVerificationToken"] = token,
        };

        var response = await client.PostAsync($"/assessment-sessions/{sessionId}/submit", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var session = db.AssessmentSessions.Single(s => s.Id == sessionId);
        Assert.Equal(AssessmentSessionStatus.TimedOut, session.Status);
    }

    [Fact]
    public async Task Submit_before_the_deadline_with_multiple_question_files_is_a_real_submission()
    {
        var (client, sessionId) = await SeedInProgressSessionAsync(DateTime.UtcNow, 90);
        var token = await ExtractAntiforgeryTokenAsync(client, $"/assessment-sessions/{sessionId}");

        var form = new Dictionary<string, string>
        {
            ["FilesJson"] = """[{"path":"ShoppingCart.cs","content":"// q1"},{"path":"Paginator.cs","content":"// q2"}]""",
            ["__RequestVerificationToken"] = token,
        };

        var response = await client.PostAsync($"/assessment-sessions/{sessionId}/submit", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var session = db.AssessmentSessions.Include(s => s.Submission).Single(s => s.Id == sessionId);
        Assert.Equal(AssessmentSessionStatus.Submitted, session.Status);
        Assert.Contains("ShoppingCart.cs", session.Submission!.SubmissionReference);
        Assert.Contains("Paginator.cs", session.Submission!.SubmissionReference);
    }
}
