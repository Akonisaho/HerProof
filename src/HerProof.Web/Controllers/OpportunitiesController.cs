using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Competencies;
using HerProof.Application.Opportunities;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Opportunities;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Candidate-facing opportunity browsing and application (docs/05 P04-P05).</summary>
[Route("opportunities")]
public class OpportunitiesController(HerProofDbContext db) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var opportunities = await db.Opportunities
            .AsNoTracking()
            .Where(o => o.Status == OpportunityStatus.Published)
            .ToListAsync(cancellationToken);

        return View(opportunities);
    }

    [HttpGet("{opportunityId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid opportunityId, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities
            .Include(o => o.RequiredCompetencies)
            .Include(o => o.EligibilityRules)
            .FirstOrDefaultAsync(o => o.Id == opportunityId && o.Status == OpportunityStatus.Published, cancellationToken);

        if (opportunity is null)
        {
            return NotFound();
        }

        var competencyNames = await db.Competencies.AsNoTracking()
            .Where(c => opportunity.RequiredCompetencies.Select(rc => rc.CompetencyId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        OpportunityEligibilitySummary? eligibility = null;
        CandidateProfile? candidateProfile = null;
        bool alreadyApplied = false;
        GapReport? gapReport = null;

        if (User.IsInRole(nameof(UserRole.Candidate)))
        {
            var userId = GetUserId();
            candidateProfile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (candidateProfile is not null)
            {
                eligibility = OpportunityEligibilitySummary.Evaluate(opportunity, CandidateEligibilityData.FromProfile(candidateProfile));
                alreadyApplied = await db.Applications.AsNoTracking()
                    .AnyAsync(a => a.OpportunityId == opportunityId && a.CandidateId == candidateProfile.Id, cancellationToken);

                var requiredCompetencyIds = opportunity.RequiredCompetencies.Select(rc => rc.CompetencyId).ToList();
                var candidateCompetencies = await db.CandidateCompetencies.AsNoTracking()
                    .Where(cc => cc.CandidateId == candidateProfile.Id && requiredCompetencyIds.Contains(cc.CompetencyId))
                    .ToDictionaryAsync(cc => cc.CompetencyId, cancellationToken);
                var learningResources = await db.LearningResources.AsNoTracking()
                    .Where(lr => requiredCompetencyIds.Contains(lr.CompetencyId))
                    .GroupBy(lr => lr.CompetencyId)
                    .ToDictionaryAsync(g => g.Key, g => g.First(), cancellationToken);

                gapReport = GapToProofEngine.Generate(opportunity, candidateProfile.Id, candidateCompetencies, competencyNames, learningResources, DateTime.UtcNow);

                // Stored for audit/history (docs/05 P05): every view generates a fresh, timestamped snapshot.
                db.GapReports.Add(gapReport);
                await db.SaveChangesAsync(cancellationToken);

                // docs/05 P16: candidate-reported progress against each recommended learning resource.
                var learningResourceIds = learningResources.Values.Select(lr => lr.Id).ToList();
                ViewBag.LearningProgressByResourceId = await db.LearningProgresses.AsNoTracking()
                    .Where(p => p.CandidateId == candidateProfile.Id && learningResourceIds.Contains(p.LearningResourceId))
                    .ToDictionaryAsync(p => p.LearningResourceId, p => p.Status, cancellationToken);
                ViewBag.CandidateCompetencyStatusByCompetencyId = candidateCompetencies
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Status);

                // docs/05 P16: a bursary application may attach the candidate's own verified passport credentials.
                var passportCredentials = await db.SkillsPassports.AsNoTracking()
                    .Include(p => p.Credentials)
                    .Where(p => p.CandidateId == candidateProfile.Id)
                    .SelectMany(p => p.Credentials)
                    .ToListAsync(cancellationToken);
                var verificationRecordsByCompetency = await db.VerificationRecords.AsNoTracking()
                    .Where(v => v.CandidateId == candidateProfile.Id)
                    .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);
                var allCompetencyNames = await db.Competencies.AsNoTracking()
                    .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
                ViewBag.OwnedCredentials = passportCredentials
                    .Select(c => (c.CredentialIdentifier, CompetencyName: verificationRecordsByCompetency.TryGetValue(c.VerificationRecordId, out var competencyId)
                        && allCompetencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)"))
                    .ToList();
            }
        }

        ViewBag.CompetencyNames = competencyNames;
        ViewBag.Eligibility = eligibility;
        ViewBag.CandidateProfileId = candidateProfile?.Id;
        ViewBag.AlreadyApplied = alreadyApplied;
        ViewBag.GapReport = gapReport;
        return View(opportunity);
    }

    [HttpPost("{opportunityId:guid}/apply")]
    [Authorize(Roles = nameof(UserRole.Candidate))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(Guid opportunityId, List<string>? selectedCredentialIdentifiers, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities
            .Include(o => o.EligibilityRules)
            .FirstOrDefaultAsync(o => o.Id == opportunityId && o.Status == OpportunityStatus.Published, cancellationToken);

        if (opportunity is null)
        {
            return NotFound();
        }

        var userId = GetUserId();
        var candidateProfile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (candidateProfile is null)
        {
            return NotFound();
        }

        var alreadyApplied = await db.Applications
            .AnyAsync(a => a.OpportunityId == opportunityId && a.CandidateId == candidateProfile.Id, cancellationToken);
        if (alreadyApplied)
        {
            return RedirectToAction(nameof(Details), new { opportunityId });
        }

        var eligibility = OpportunityEligibilitySummary.Evaluate(opportunity, CandidateEligibilityData.FromProfile(candidateProfile));
        if (eligibility.IsBlockedByHardFilter)
        {
            // Defence in depth: the view already hides/disables Apply in this case.
            return BadRequest("This opportunity has eligibility requirements you do not currently meet.");
        }

        var ownedCredentials = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .Where(p => p.CandidateId == candidateProfile.Id)
            .SelectMany(p => p.Credentials.Select(c => c.CredentialIdentifier))
            .ToListAsync(cancellationToken);
        var attachedCredentials = AppliedCredentialSelector.Select(ownedCredentials, selectedCredentialIdentifiers ?? []);
        var sharedCredentialsJson = JsonSerializer.Serialize(attachedCredentials);

        db.Applications.Add(new CandidateApplication(opportunityId, candidateProfile.Id, sharedCredentialsJson, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { opportunityId });
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
