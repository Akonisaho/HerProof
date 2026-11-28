using System.Security.Claims;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.ViewComponents;

/// <summary>Unread-count badge for the candidate mailbox icon in the navbar - same shape as
/// AssessorInboxBadgeViewComponent, backed by Notification.ReadAtUtc instead of the audit log.</summary>
public class NotificationBellViewComponent(HerProofDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = Guid.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var candidateId = await db.CandidateProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

        var unreadCount = candidateId is null
            ? 0
            : await db.Notifications.AsNoTracking()
                .CountAsync(n => n.CandidateId == candidateId && n.ReadAtUtc == null);

        return View(unreadCount);
    }
}
