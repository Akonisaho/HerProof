using System.Security.Claims;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("notifications")]
public class NotificationsController(HerProofDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.CandidateId == candidateId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // So a culture-fit invite or job offer notification can show real Accept/Decline buttons -
        // or her already-given answer - without a second round trip per row.
        var outreachIds = notifications.Where(n => n.RelatedOutreachId is not null).Select(n => n.RelatedOutreachId!.Value).ToList();
        ViewBag.OutreachById = await db.CandidateOutreaches.AsNoTracking()
            .Where(o => outreachIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        return View(notifications);
    }

    /// <summary>Her real answer to a specific culture-fit interview invite or job offer - never
    /// silently assumed, and only ever settable once (CandidateOutreach.Respond is a no-op on a
    /// second call) so a recruiter never sees a response she didn't actually give.</summary>
    [HttpPost("outreach/{outreachId:guid}/respond")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondToOutreach(Guid outreachId, OutreachResponseStatus status, string? responseMessage, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var outreach = await db.CandidateOutreaches
            .FirstOrDefaultAsync(o => o.Id == outreachId && o.CandidateId == candidateId, cancellationToken);
        if (outreach is null)
        {
            return NotFound();
        }

        outreach.Respond(status, responseMessage, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{notificationId:guid}/mark-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.CandidateId == candidateId, cancellationToken);
        if (notification is null)
        {
            return NotFound();
        }

        notification.MarkRead(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var unread = await db.Notifications
            .Where(n => n.CandidateId == candidateId && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<Guid?> GetCandidateProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }
}
