using System.Text.Json;
using HerProof.Application.Authorization;
using HerProof.Domain.Opportunities;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Recruiter and Partner dashboard (docs/05 P04): create and manage an organisation's own
/// opportunities. Recruiters post Jobs/Internships/Learnerships, Partners post Bursaries, but either
/// role may use this controller — ownership (which organisation) is what's actually enforced.</summary>
[Authorize(Roles = "Recruiter,Partner")]
[Route("organisations/{organisationId:guid}/opportunities")]
public class OrganisationOpportunitiesController(HerProofDbContext db, IResourceAuthorizer resourceAuthorizer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid organisationId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunities = await db.Opportunities
            .AsNoTracking()
            .Where(o => o.OrganisationId == organisationId)
            .ToListAsync(cancellationToken);

        ViewBag.OrganisationId = organisationId;
        return View(opportunities);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid organisationId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        return View(new CreateOpportunityViewModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid organisationId, CreateOpportunityViewModel model, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var deadlineUtc = model.DeadlineUtc.HasValue ? DateTime.SpecifyKind(model.DeadlineUtc.Value, DateTimeKind.Utc) : (DateTime?)null;
        var opportunity = new Opportunity(organisationId, model.Title, model.Description, model.OpportunityType, model.Location, deadlineUtc);
        opportunity.SetCompensationAndExperience(model.SalaryMin, model.SalaryMax, model.ExperienceYearsRequired);

        // Published immediately on creation: a recruiter/partner can still Close it from the detail
        // page, but there's no separate draft-review step to trip over during a live demo.
        opportunity.Publish();

        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { organisationId, opportunityId = opportunity.Id });
    }

    [HttpGet("{opportunityId:guid}")]
    public async Task<IActionResult> Details(Guid organisationId, Guid opportunityId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await db.Opportunities
            .Include(o => o.RequiredCompetencies)
            .Include(o => o.EligibilityRules)
            .FirstOrDefaultAsync(o => o.Id == opportunityId && o.OrganisationId == organisationId, cancellationToken);

        if (opportunity is null)
        {
            return NotFound();
        }

        var competencies = await db.Competencies.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        return View(new OrganisationOpportunityDetailViewModel
        {
            Opportunity = opportunity,
            AvailableCompetencies = competencies.Select(c => (c.Id, c.Name)).ToList(),
            CompetencyNames = competencies.ToDictionary(c => c.Id, c => c.Name),
        });
    }

    [HttpPost("{opportunityId:guid}/competencies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCompetency(Guid organisationId, Guid opportunityId, AddCompetencyRequirementViewModel model, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await db.Opportunities
            .Include(o => o.RequiredCompetencies)
            .FirstOrDefaultAsync(o => o.Id == opportunityId && o.OrganisationId == organisationId, cancellationToken);

        if (opportunity is null)
        {
            return NotFound();
        }

        // Explicit Add(): our entities pre-assign Id in the constructor, so EF can't tell "new" from
        // "existing" purely from graph fixup on an already-tracked parent and defaults to Modified.
        var requirement = opportunity.RequireCompetency(model.CompetencyId, model.RequiredLevel, model.IsMandatory);
        db.Add(requirement);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { organisationId, opportunityId });
    }

    [HttpPost("{opportunityId:guid}/eligibility-rules")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEligibilityRule(Guid organisationId, Guid opportunityId, AddEligibilityRuleViewModel model, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await db.Opportunities
            .Include(o => o.EligibilityRules)
            .FirstOrDefaultAsync(o => o.Id == opportunityId && o.OrganisationId == organisationId, cancellationToken);

        if (opportunity is null)
        {
            return NotFound();
        }

        var rule = opportunity.AddEligibilityRule(model.RuleType, model.Operator, model.Value, model.IsHardFilter, model.Explanation);
        db.Add(rule);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { organisationId, opportunityId });
    }

    [HttpPost("{opportunityId:guid}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid organisationId, Guid opportunityId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId && o.OrganisationId == organisationId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        opportunity.Publish();
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { organisationId, opportunityId });
    }

    [HttpPost("{opportunityId:guid}/close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid organisationId, Guid opportunityId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId && o.OrganisationId == organisationId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        opportunity.Close();
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { organisationId, opportunityId });
    }

    [HttpGet("{opportunityId:guid}/applications")]
    public async Task<IActionResult> Applications(Guid organisationId, Guid opportunityId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await db.Opportunities.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == opportunityId && o.OrganisationId == organisationId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.OpportunityId == opportunityId)
            .OrderByDescending(a => a.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();
        var candidateNames = await db.CandidateProfiles.AsNoTracking()
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => new { p.Id, u.FullName })
            .Where(x => candidateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var credentialsByIdentifier = await db.PassportCredentials.AsNoTracking()
            .ToDictionaryAsync(c => c.CredentialIdentifier, c => c.VerificationRecordId, cancellationToken);
        var verificationRecordsByCompetency = await db.VerificationRecords.AsNoTracking()
            .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);

        var items = applications.Select(a =>
        {
            var identifiers = JsonSerializer.Deserialize<List<string>>(a.SharedCredentialsJson) ?? [];
            var attached = identifiers
                .Select(id => (CredentialIdentifier: id, CompetencyName: credentialsByIdentifier.TryGetValue(id, out var verificationRecordId)
                    && verificationRecordsByCompetency.TryGetValue(verificationRecordId, out var competencyId)
                    && competencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)"))
                .ToList();

            return new ApplicationReviewItem
            {
                Application = a,
                CandidateName = candidateNames.GetValueOrDefault(a.CandidateId, "(unknown)"),
                AttachedCredentials = attached,
            };
        }).ToList();

        return View(new OrganisationApplicationsViewModel { Opportunity = opportunity, Applications = items });
    }

    [HttpPost("{opportunityId:guid}/applications/{applicationId:guid}/mark-reviewed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkApplicationReviewed(Guid organisationId, Guid opportunityId, Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var application = await db.Applications
            .Join(db.Opportunities, a => a.OpportunityId, o => o.Id, (a, o) => new { Application = a, o.OrganisationId })
            .Where(x => x.Application.Id == applicationId && x.Application.OpportunityId == opportunityId && x.OrganisationId == organisationId)
            .Select(x => x.Application)
            .FirstOrDefaultAsync(cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        application.MarkReviewed();
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Applications), new { organisationId, opportunityId });
    }
}
