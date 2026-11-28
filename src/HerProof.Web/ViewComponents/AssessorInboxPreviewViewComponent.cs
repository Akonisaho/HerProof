using System.Security.Claims;
using HerProof.Domain.Competencies;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Verification;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.ViewComponents;

/// <summary>What you see when you actually open the mail icon, like a real email client's unread
/// preview - one row per specific submission ("Anko submitted - Error handling"), newest first,
/// each clicking straight into that exact evidence pack. The grouped-by-candidate grid at /assessor
/// is the folder view; this is the message list that tells you what's new and where to click.</summary>
public class AssessorInboxPreviewViewComponent(HerProofDbContext db) : ViewComponent
{
    private const int MaxRows = 6;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var assessorId = Guid.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var pending = await db.CandidateCompetencies.AsNoTracking()
            .Where(cc => cc.Status == CandidateCompetencyStatus.EvidenceSubmitted
                || cc.Status == CandidateCompetencyStatus.UnderAssessment
                || cc.Status == CandidateCompetencyStatus.AssessmentCompleted)
            .ToListAsync();

        var candidateIds = pending.Select(p => p.CandidateId).Distinct().ToList();
        var competencyIds = pending.Select(p => p.CompetencyId).Distinct().ToList();

        var candidateNames = await db.CandidateProfiles.AsNoTracking()
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => new { p.Id, u.FullName })
            .Where(x => candidateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName);
        var competencyNames = await db.Competencies.AsNoTracking()
            .Where(c => competencyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var viewedLog = await db.AuditLog.AsNoTracking()
            .Where(a => a.ActorUserId == assessorId && a.Action == "EvidencePackViewed" && candidateIds.Contains(a.EntityId))
            .ToListAsync();

        var rows = pending
            .Select(cc => new InboxMessageRow
            {
                CandidateId = cc.CandidateId,
                CompetencyId = cc.CompetencyId,
                CandidateName = candidateNames.GetValueOrDefault(cc.CandidateId, "(unknown)"),
                CompetencyName = competencyNames.GetValueOrDefault(cc.CompetencyId, "(unknown)"),
                UpdatedAtUtc = cc.UpdatedAtUtc,
                IsUnread = !viewedLog.Any(a => a.EntityId == cc.CandidateId
                    && a.DetailJson.Contains(cc.CompetencyId.ToString())
                    && a.AtUtc >= cc.UpdatedAtUtc),
            })
            .OrderByDescending(r => r.IsUnread)
            .ThenByDescending(r => r.UpdatedAtUtc)
            .Take(MaxRows)
            .ToList();

        return View(rows);
    }
}
