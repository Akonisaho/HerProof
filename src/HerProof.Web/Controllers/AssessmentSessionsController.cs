using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Assessments;
using HerProof.Domain.Assessments;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Candidate-facing practical assessment session (docs/05 P13, docs/01 section 10 state
/// machine). The Electron agent (P12) is where the real timed, monitored workspace lives; this is
/// the server side it talks to — consent, session lifecycle, submission, and the token the agent
/// uses to post integrity events (STUB(HP-025) until real OAuth2 PKCE).</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("assessment-sessions")]
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

        var assessment = await db.Assessments.AsNoTracking().FirstAsync(a => a.Id == session.AssessmentId, cancellationToken);
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

        // STUB(HP-028): single-file plain-text submission, not the zipped workspace docs/07 section 9
        // describes — matches the Electron agent's current plain-textarea stand-in for Monaco (HP-026).
        var submissionReference = JsonSerializer.Serialize(new[] { new SubmittedFile("Solution.cs", model.Code) });
        var submission = session.Submit(submissionReference, DateTime.UtcNow);
        db.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { sessionId });
    }

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
