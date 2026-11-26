using System.Security.Claims;
using HerProof.Domain.Competencies;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.ViewComponents;

/// <summary>Unread-count badge for the "Assessor dashboard" nav link, backed by the same
/// audit-log-based read tracking as AssessorDashboardController.Index — kept in sync there.</summary>
public class AssessorInboxBadgeViewComponent(HerProofDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var assessorId = Guid.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var pending = await db.CandidateCompetencies.AsNoTracking()
            .Where(cc => cc.Status == CandidateCompetencyStatus.EvidenceSubmitted
                || cc.Status == CandidateCompetencyStatus.UnderAssessment
                || cc.Status == CandidateCompetencyStatus.AssessmentCompleted)
            .ToListAsync();

        var candidateIds = pending.Select(p => p.CandidateId).Distinct().ToList();
        var viewedLog = await db.AuditLog.AsNoTracking()
            .Where(a => a.ActorUserId == assessorId && a.Action == "EvidencePackViewed" && candidateIds.Contains(a.EntityId))
            .ToListAsync();

        var unreadCount = pending.Count(cc => !viewedLog.Any(a => a.EntityId == cc.CandidateId
            && a.DetailJson.Contains(cc.CompetencyId.ToString())
            && a.AtUtc >= cc.UpdatedAtUtc));

        return View(unreadCount);
    }
}
