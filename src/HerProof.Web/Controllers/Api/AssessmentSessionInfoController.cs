using System.Text.Json;
using HerProof.Application.Assessments;
using HerProof.Domain.Assessments;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers.Api;

public record SessionInfoResponse(string Status, DateTime? StartedAtUtc, int DurationMinutes, DateTime ServerTimeUtc);

public record SubmitCodeRequest(string Code);

/// <summary>HP-026, closed: the Electron agent previously had no server-authoritative timer — only a
/// client-side stopwatch counting up from zero with no real time limit or expiry. Same bearer-token
/// auth as IntegrityEventsController (docs/07 section 6); STUB(HP-025) until real OAuth2 PKCE.
/// The server, not the Electron client, decides whether a submission is on-time or a timeout: the
/// client's own countdown is only ever a UI convenience, never the source of truth.</summary>
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/assessment-sessions/{sessionId:guid}")]
public class AssessmentSessionInfoController(HerProofDbContext db, ISessionEventTokenService tokenService) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> GetInfo(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await AuthenticateAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Unauthorized();
        }

        var assessment = await db.Assessments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == session.AssessmentId, cancellationToken);
        if (assessment is null)
        {
            return NotFound();
        }

        return Ok(new SessionInfoResponse(session.Status.ToString(), session.StartedAtUtc, assessment.DurationMinutes, DateTime.UtcNow));
    }

    [HttpPost("code")]
    public async Task<IActionResult> SubmitCode(Guid sessionId, [FromBody] SubmitCodeRequest request, CancellationToken cancellationToken)
    {
        var session = await AuthenticateAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Unauthorized();
        }

        if (session.Status != AssessmentSessionStatus.InProgress || session.StartedAtUtc is null)
        {
            return Conflict();
        }

        var now = DateTime.UtcNow;
        var assessment = await db.Assessments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == session.AssessmentId, cancellationToken);
        if (assessment is null)
        {
            return NotFound();
        }

        var submissionReference = JsonSerializer.Serialize(new[] { new { FileName = "Solution.cs", Content = request.Code } });
        var deadline = session.StartedAtUtc.Value.AddMinutes(assessment.DurationMinutes);

        // Server-authoritative: the time limit is checked here, against the server clock and the
        // StartedAtUtc the server itself recorded, not against anything the client sends.
        var submission = now <= deadline
            ? session.Submit(submissionReference, now)
            : session.TimeOut(submissionReference, now);
        db.Add(submission);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = session.Status.ToString(), submittedAtUtc = submission.SubmittedAtUtc });
    }

    private async Task<AssessmentSession?> AuthenticateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..];
        var tokenSessionId = tokenService.Validate(token, DateTime.UtcNow);
        if (tokenSessionId != sessionId)
        {
            return null;
        }

        return await db.AssessmentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }
}
