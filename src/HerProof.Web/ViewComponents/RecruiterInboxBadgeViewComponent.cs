using System.Security.Claims;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.ViewComponents;

/// <summary>Unread-count badge for the Recruiter/Partner "Responses" mailbox icon - same shape as
/// NotificationBellViewComponent, backed by CandidateOutreach.RecruiterSeenAtUtc instead of
/// Notification.ReadAtUtc, since outreach responses are a separate, real-world mailbox.</summary>
public class RecruiterInboxBadgeViewComponent(HerProofDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var recruiterUserId = Guid.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var unreadCount = await db.CandidateOutreaches.AsNoTracking()
            .CountAsync(o => o.RecruiterUserId == recruiterUserId
                && o.ResponseStatus != OutreachResponseStatus.Pending
                && o.RecruiterSeenAtUtc == null);

        return View(unreadCount);
    }
}
