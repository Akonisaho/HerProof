using System.Text.Json;
using HerProof.Application.Assessments;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers.Api;

public record IncomingEventDto(string EventType, DateTime OccurredAt, Dictionary<string, object>? Metadata, int ClientSeq);

public record PostEventsRequest(List<IncomingEventDto> Events);

/// <summary>What the Electron agent's SyncClient posts to (docs/07 section 6, docs/05 P13). Token-
/// authenticated, not cookie-authenticated — STUB(HP-025): a session-scoped HMAC token stands in for
/// real OAuth2 PKCE for now. Dedupe and gap detection are delegated entirely to
/// IntegrityEventIngestion (Application layer, pure logic, already tested) — this controller is only
/// I/O plumbing around it.</summary>
[ApiController]
[AllowAnonymous]
// Bearer-token authenticated, not cookie-authenticated (docs/03 section 3's antiforgery requirement
// is for cookie-based browser forms; a token-bearing API client is inherently immune to CSRF since it
// carries no ambient browser credential for a malicious page to ride along with).
[IgnoreAntiforgeryToken]
[Route("api/assessment-sessions/{sessionId:guid}/events")]
public class IntegrityEventsController(HerProofDbContext db, ISessionEventTokenService tokenService, ILogger<IntegrityEventsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(Guid sessionId, [FromBody] PostEventsRequest request, CancellationToken cancellationToken)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var token = authHeader["Bearer ".Length..];
        var tokenSessionId = tokenService.Validate(token, DateTime.UtcNow);
        if (tokenSessionId != sessionId)
        {
            return Unauthorized();
        }

        var session = await db.AssessmentSessions
            .Include(s => s.IntegrityEvents)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var incoming = request.Events
            .Select(e => new IncomingIntegrityEvent(e.EventType, e.OccurredAt, JsonSerializer.Serialize(e.Metadata ?? []), e.ClientSeq.ToString()))
            .ToList();
        var alreadyPersisted = session.IntegrityEvents.Select(e => e.ClientSeq).ToList();

        var result = IntegrityEventIngestion.Process(incoming, alreadyPersisted);

        foreach (var toPersist in result.ToPersist)
        {
            var persisted = session.RecordIntegrityEvent(toPersist.EventType, toPersist.OccurredAtUtc, toPersist.MetadataJson, toPersist.ClientSeq);
            db.Add(persisted);
        }

        if (result.Gaps.Count > 0)
        {
            // A gap is a flag for human review, never a conclusion (docs/07 section 7) — logged, not acted on.
            logger.LogInformation("Session {SessionId} has integrity event sequence gaps: {Gaps}", sessionId, string.Join(',', result.Gaps));
        }

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { acceptedClientSeqs = result.AcceptedClientSeqs.Select(int.Parse).ToList() });
    }
}
