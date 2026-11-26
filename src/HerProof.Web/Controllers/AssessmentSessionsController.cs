using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Assessments;
using HerProof.Domain.Assessments;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Filters;
using HerProof.Web.Models.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

public record RecordBrowserEventRequest(string EventType);

/// <summary>Candidate-facing practical assessment session (docs/05 P13, docs/01 section 10 state
/// machine): consent, the real timed/monitored workspace itself (editor, timer, integrity log —
/// all in the browser), submission, and the Electron agent's legacy token-based path (STUB(HP-025)
/// until real OAuth2 PKCE) for anyone who still launches that instead.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("assessment-sessions")]
[TypeFilter(typeof(RequireCompleteCandidateProfileFilter))]
public class AssessmentSessionsController(HerProofDbContext db, ISessionEventTokenService tokenService) : Controller
{
    [HttpGet("browse")]
    public async Task<IActionResult> Browse(CancellationToken cancellationToken)
    {
        var assessments = await db.Assessments.AsNoTracking()
            .Where(a => a.Status == AssessmentStatus.Published)
            .ToListAsync(cancellationToken);
        return View(assessments);
    }

    [HttpPost("start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid assessmentId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var assessment = await db.Assessments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.Status == AssessmentStatus.Published, cancellationToken);
        if (candidateId is null || assessment is null)
        {
            return NotFound();
        }

        var session = new AssessmentSession(assessmentId, candidateId.Value);
        db.AssessmentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { sessionId = session.Id });
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Details(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await LoadOwnedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var assessment = await db.Assessments.AsNoTracking()
            .Include(a => a.Questions)
            .FirstAsync(a => a.Id == session.AssessmentId, cancellationToken);
        ViewBag.Assessment = assessment;

        if (session.Status == AssessmentSessionStatus.ConsentGiven)
        {
            ViewBag.EventToken = tokenService.Issue(session.Id, DateTime.UtcNow);
        }

        return View(session);
    }

    [HttpPost("{sessionId:guid}/consent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Consent(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await LoadOwnedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        session.GiveConsent(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { sessionId });
    }

    [HttpPost("{sessionId:guid}/begin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Begin(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await LoadOwnedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        session.Start(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { sessionId });
    }

    [HttpPost("{sessionId:guid}/submit")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("analysis")]
    public async Task<IActionResult> Submit(Guid sessionId, SubmitWorkViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Details(sessionId, cancellationToken);
        }

        var session = await LoadOwnedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        if (session.Status != AssessmentSessionStatus.InProgress || session.StartedAtUtc is null)
        {
            return Conflict();
        }

        var assessment = await db.Assessments.AsNoTracking().FirstAsync(a => a.Id == session.AssessmentId, cancellationToken);

        // STUB(HP-028): plain-text files, not the zipped workspace docs/07 section 9 describes.
        var files = ParseSubmittedFiles(model);
        var submissionReference = JsonSerializer.Serialize(files);

        var now = DateTime.UtcNow;
        var deadline = session.StartedAtUtc.Value.AddMinutes(assessment.DurationMinutes);

        // Server-authoritative (HP-026, closed for the Electron path; the same rule now applies
        // here too): the server's own clock and StartedAtUtc decide Submitted vs TimedOut, never
        // anything the browser's own countdown claims.
        var submission = now <= deadline
            ? session.Submit(submissionReference, now)
            : session.TimeOut(submissionReference, now);

        db.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { sessionId });
    }

    /// <summary>Browser-side integrity events (HP-024, closed for text mode's sibling): the same
    /// fixed event list the Electron agent uses (docs/07 section 5), posted from this cookie-
    /// authenticated session instead of a bearer token, since the browser never leaves it. A tab
    /// structurally can't see window-close or other-app focus, so only tab-visibility, network and
    /// device-presence events are ever recorded here — never a false claim of OS-level monitoring.</summary>
    [HttpPost("{sessionId:guid}/events")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordEvent(Guid sessionId, [FromBody] RecordBrowserEventRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedBrowserEventTypes.Contains(request.EventType))
        {
            return BadRequest();
        }

        var session = await LoadOwnedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var persisted = session.RecordIntegrityEvent(request.EventType, DateTime.UtcNow, "{}", Guid.NewGuid().ToString());
        db.Add(persisted);
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    private static readonly HashSet<string> AllowedBrowserEventTypes =
    [
        "focus_lost", "focus_regained", "network_lost", "network_restored",
        "camera_connected", "camera_disconnected", "microphone_connected", "microphone_disconnected",
    ];

    private static List<SubmittedFile> ParseSubmittedFiles(SubmitWorkViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.FilesJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<SubmittedFile>>(model.FilesJson, JsonSerializerOptions);
                if (parsed is { Count: > 0 })
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fall through to the legacy single-file path below rather than failing the submit.
            }
        }

        return [new SubmittedFile("Solution.cs", model.Code ?? string.Empty)];
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private async Task<AssessmentSession?> LoadOwnedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return null;
        }

        return await db.AssessmentSessions
            .Include(s => s.Submission)
            .Include(s => s.IntegrityEvents)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CandidateId == candidateId, cancellationToken);
    }

    private async Task<Guid?> GetCandidateProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }
}
