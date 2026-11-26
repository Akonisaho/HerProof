using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Competencies;
using HerProof.Application.Opportunities;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Opportunities;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Filters;
using HerProof.Web.Models.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Candidate-facing opportunity browsing and application (docs/05 P04-P05). Anonymous
/// visitors can still browse (marketing funnel); a logged-in Candidate with an incomplete profile is
/// redirected to complete it first (RequireCompleteCandidateProfileFilter).</summary>
[Route("opportunities")]
[TypeFilter(typeof(RequireCompleteCandidateProfileFilter))]
public class OpportunitiesController(HerProofDbContext db) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(OpportunitySearchViewModel search, CancellationToken cancellationToken)
    {
        var opportunities = await db.Opportunities
            .AsNoTracking()
            .Include(o => o.RequiredCompetencies)
            .Where(o => o.Status == OpportunityStatus.Published)
            .OrderBy(o => o.DeadlineUtc)
            .ToListAsync(cancellationToken);

        var organisationIds = opportunities.Select(o => o.OrganisationId).Distinct().ToList();
        var organisationNames = await db.Organisations.AsNoTracking()
            .Where(o => organisationIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        var allCompetencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        // Server-side so "search for a skill" actually matches the opportunity's required
        // competencies, not just title/description text a candidate wouldn't think to type.
        if (!string.IsNullOrWhiteSpace(search.Q))
        {
            var q = search.Q.Trim();
            opportunities = opportunities.Where(o =>
                o.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || o.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                || o.RequiredCompetencies.Any(rc => allCompetencyNames.GetValueOrDefault(rc.CompetencyId, "").Contains(q, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (search.Type is not null)
        {
            opportunities = opportunities.Where(o => o.OpportunityType == search.Type).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search.Location))
        {
            var location = search.Location.Trim();
            opportunities = opportunities.Where(o => o.Location != null && o.Location.Contains(location, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (search.MaxExperienceYears is not null)
        {
            opportunities = opportunities.Where(o => o.ExperienceYearsRequired is null || o.ExperienceYearsRequired <= search.MaxExperienceYears).ToList();
        }

        if (search.HasSalaryDisclosed)
        {
            opportunities = opportunities.Where(o => o.SalaryMin is not null || o.SalaryMax is not null).ToList();
        }

        var items = opportunities
            .Select(o => new OpportunityListItem { Opportunity = o, OrganisationName = organisationNames.GetValueOrDefault(o.OrganisationId, "Unknown organisation") })
            .ToList();

        ViewBag.Search = search;

        return View(items);
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

        var organisationName = await db.Organisations.AsNoTracking()
            .Where(o => o.Id == opportunity.OrganisationId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown organisation";

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
                var candidateLearningResources = await db.LearningResources.AsNoTracking()
                    .Where(lr => requiredCompetencyIds.Contains(lr.CompetencyId))
                    .ToListAsync(cancellationToken);
                var learningResources = candidateLearningResources
                    .GroupBy(lr => lr.CompetencyId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(lr => lr.TargetLevel).ThenBy(lr => lr.Title).First());

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

        ViewBag.OrganisationName = organisationName;
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

        // Evidence before verification, platform-wide (CLAUDE.md rule 1): a candidate with zero
        // verified competencies has no proof to stand behind an application yet. Defence in depth —
        // the view already hides the Apply button in this case.
        if (ownedCredentials.Count == 0)
        {
            return BadRequest("You need at least one Assessor-verified competency on your Skills Passport before you can apply. Submit a repository or take a practical assessment first.");
        }

        var attachedCredentials = AppliedCredentialSelector.Select(ownedCredentials, selectedCredentialIdentifiers ?? []);
        var sharedCredentialsJson = JsonSerializer.Serialize(attachedCredentials);

        db.Applications.Add(new CandidateApplication(opportunityId, candidateProfile.Id, sharedCredentialsJson, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { opportunityId });
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
