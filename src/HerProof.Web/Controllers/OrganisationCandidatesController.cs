using System.Security.Claims;
using HerProof.Domain.Identity;
using HerProof.Domain.Notifications;
using HerProof.Domain.Passports;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

public class ReachOutViewModel
{
    public OutreachType OutreachType { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Browse verified candidates independent of any opportunity (docs/05 plan, recruiter
/// browse/reach-out slice): a Recruiter or Bursary Partner can write a real message to a candidate's
/// already verified profile without her first having applied anywhere - inviting her to a
/// culture-fit interview, or offering her the job, with real details, never a bare "like" with
/// nothing behind it. Allow-list discipline matches PassportVerificationController (CLAUDE.md rule 9)
/// - never repositories, recordings, transcripts, integrity events, scores or contact details, only
/// what a public passport link would show plus the profile fields the candidate filled in herself.</summary>
[Authorize(Roles = "Recruiter,Partner")]
[Route("candidates")]
public class OrganisationCandidatesController(HerProofDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var recruiterUserId = GetUserId();

        var passports = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .Where(p => p.Credentials.Any(c => c.CredentialStatus == CredentialStatus.Active))
            .ToListAsync(cancellationToken);

        var candidateIds = passports.Select(p => p.CandidateId).ToList();

        var candidateUsers = await db.CandidateProfiles.AsNoTracking()
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => new { Profile = p, u.FullName })
            .Where(x => candidateIds.Contains(x.Profile.Id))
            .ToDictionaryAsync(x => x.Profile.Id, x => x, cancellationToken);

        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var verificationRecords = await db.VerificationRecords.AsNoTracking()
            .Where(v => candidateIds.Contains(v.CandidateId))
            .ToDictionaryAsync(v => v.Id, v => (v.CompetencyId, v.FinalLevel), cancellationToken);

        var repositoryCounts = await db.Repositories.AsNoTracking()
            .Where(r => candidateIds.Contains(r.CandidateId))
            .GroupBy(r => r.CandidateId)
            .Select(g => new { CandidateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CandidateId, x => x.Count, cancellationToken);

        var preferredCompetencies = await db.CandidatePreferredCompetencies.AsNoTracking()
            .Where(pc => candidateIds.Contains(pc.CandidateId))
            .ToListAsync(cancellationToken);

        var existingOutreach = await db.CandidateOutreaches.AsNoTracking()
            .Where(o => o.RecruiterUserId == recruiterUserId && candidateIds.Contains(o.CandidateId))
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var outreachByCandidate = existingOutreach
            .GroupBy(o => o.CandidateId)
            .ToDictionary(g => g.Key, g => g.Select(o => (o.OutreachType, o.CreatedAtUtc, o.ResponseStatus)).ToList());

        var items = passports
            .Where(p => candidateUsers.ContainsKey(p.CandidateId))
            .Select(p =>
            {
                var candidate = candidateUsers[p.CandidateId];
                var verified = p.Credentials
                    .Where(c => c.CredentialStatus == CredentialStatus.Active)
                    .Select(c => verificationRecords.TryGetValue(c.VerificationRecordId, out var vr)
                        ? (CompetencyName: competencyNames.GetValueOrDefault(vr.CompetencyId, "(unknown)"), Level: vr.FinalLevel)
                        : (CompetencyName: "(unknown)", Level: (int?)null))
                    .ToList();
                var preferred = preferredCompetencies
                    .Where(pc => pc.CandidateId == p.CandidateId)
                    .Select(pc => competencyNames.GetValueOrDefault(pc.CompetencyId, "(unknown)"))
                    .ToList();

                return new OrganisationCandidateListItem
                {
                    CandidateId = p.CandidateId,
                    FullName = candidate.FullName,
                    Province = candidate.Profile.Province,
                    EducationLevel = candidate.Profile.EducationLevel,
                    CareerInterests = candidate.Profile.CareerInterests,
                    GitHubProfile = candidate.Profile.GitHubProfile,
                    LinkedInProfile = candidate.Profile.LinkedInProfile,
                    VerifiedCompetencies = verified,
                    PreferredCompetencyNames = preferred,
                    RepositoryCount = repositoryCounts.GetValueOrDefault(p.CandidateId),
                    OutreachSent = outreachByCandidate.GetValueOrDefault(p.CandidateId, []),
                };
            })
            .OrderByDescending(i => i.VerifiedCompetencies.Count)
            .ToList();

        return View(items);
    }

    /// <summary>The recruiter/partner equivalent of the candidate's notification bell - until now
    /// there was no surface for a recruiter to even know she'd responded to an outreach message at
    /// all, other than stumbling back onto Browse candidates and re-reading every pill. Opening this
    /// marks every response here as seen, the same "mark all read" idiom NotificationsController
    /// already uses for the candidate side.</summary>
    [HttpGet("responses")]
    public async Task<IActionResult> Responses(CancellationToken cancellationToken)
    {
        var recruiterUserId = GetUserId();

        var outreach = await db.CandidateOutreaches
            .Where(o => o.RecruiterUserId == recruiterUserId && o.ResponseStatus != OutreachResponseStatus.Pending)
            .OrderByDescending(o => o.RespondedAtUtc)
            .ToListAsync(cancellationToken);

        var candidateIds = outreach.Select(o => o.CandidateId).Distinct().ToList();
        var candidateNames = await db.CandidateProfiles.AsNoTracking()
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => new { p.Id, u.FullName })
            .Where(x => candidateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var rows = outreach
            .Select(o => new OrganisationResponseRow
            {
                CandidateId = o.CandidateId,
                CandidateName = candidateNames.GetValueOrDefault(o.CandidateId, "(unknown)"),
                OutreachType = o.OutreachType,
                Message = o.Message,
                SentAtUtc = o.CreatedAtUtc,
                ResponseStatus = o.ResponseStatus,
                ResponseMessage = o.ResponseMessage,
                RespondedAtUtc = o.RespondedAtUtc,
            })
            .ToList();

        var now = DateTime.UtcNow;
        foreach (var o in outreach)
        {
            o.MarkSeenByRecruiter(now);
        }
        await db.SaveChangesAsync(cancellationToken);

        return View(rows);
    }

    [HttpPost("{candidateId:guid}/reach-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReachOut(Guid candidateId, ReachOutViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Message))
        {
            TempData["CandidatesError"] = "Write a message before sending.";
            return RedirectToAction(nameof(Index));
        }

        var recruiterUserId = GetUserId();
        var organisationName = await db.OrganisationMembers.AsNoTracking()
            .Where(m => m.UserId == recruiterUserId)
            .Join(db.Organisations.AsNoTracking(), m => m.OrganisationId, o => o.Id, (m, o) => o.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "An employer";

        var outreach = new CandidateOutreach(recruiterUserId, candidateId, model.OutreachType, model.Message, DateTime.UtcNow);
        db.Add(outreach);

        var (notificationType, label) = model.OutreachType == OutreachType.JobOffer
            ? (NotificationType.JobOffer, "offered you a position")
            : (NotificationType.CultureFitInterviewInvite, "invited you to a culture-fit interview");
        db.Add(new Notification(candidateId, notificationType, $"{organisationName} {label}: {model.Message}", relatedCompetencyId: null, DateTime.UtcNow, relatedOutreachId: outreach.Id));

        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
