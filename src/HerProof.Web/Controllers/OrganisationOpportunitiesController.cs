using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Authorization;
using HerProof.Application.Opportunities;
using HerProof.Domain.Assessments;
using HerProof.Domain.Notifications;
using HerProof.Domain.Opportunities;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HerProof.Web.Controllers;

/// <summary>Recruiter and Partner dashboard (docs/05 P04): create and manage an organisation's own
/// opportunities. Recruiters post Jobs/Internships/Learnerships, Partners post Bursaries, but either
/// role may use this controller — ownership (which organisation) is what's actually enforced.</summary>
[Authorize(Roles = "Recruiter,Partner")]
[Route("organisations/{organisationId:guid}/opportunities")]
public class OrganisationOpportunitiesController(HerProofDbContext db, IResourceAuthorizer resourceAuthorizer, IConfiguration configuration) : Controller
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
        await BroadcastNewOpportunityAsync(opportunity, cancellationToken);
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

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ownPublishedAssessments = await db.Assessments.AsNoTracking()
            .Where(a => a.CreatedBy == userId && a.Status == AssessmentStatus.Published)
            .Select(a => new { a.Id, a.Title, a.AssessmentType, a.DurationMinutes })
            .ToListAsync(cancellationToken);

        (string Title, string AssessmentType, int DurationMinutes)? linkedAssessment = null;
        if (opportunity.LinkedAssessmentId is Guid linkedId)
        {
            var linked = await db.Assessments.AsNoTracking()
                .Where(a => a.Id == linkedId)
                .Select(a => new { a.Title, a.AssessmentType, a.DurationMinutes })
                .FirstOrDefaultAsync(cancellationToken);
            if (linked is not null)
            {
                linkedAssessment = (linked.Title, linked.AssessmentType, linked.DurationMinutes);
            }
        }

        return View(new OrganisationOpportunityDetailViewModel
        {
            Opportunity = opportunity,
            AvailableCompetencies = competencies.Select(c => (c.Id, c.Name)).ToList(),
            CompetencyNames = competencies.ToDictionary(c => c.Id, c => c.Name),
            OwnPublishedAssessments = ownPublishedAssessments.Select(a => (a.Id, a.Title, a.AssessmentType, a.DurationMinutes)).ToList(),
            LinkedAssessment = linkedAssessment,
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

    [HttpPost("{opportunityId:guid}/link-assessment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkAssessment(Guid organisationId, Guid opportunityId, Guid assessmentId, CancellationToken cancellationToken)
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

        // Only a Published assessment this recruiter/partner actually owns can be linked - never
        // someone else's draft, and never something a candidate could click into unfinished.
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ownsAssessment = await db.Assessments.AsNoTracking()
            .AnyAsync(a => a.Id == assessmentId && a.CreatedBy == userId && a.Status == AssessmentStatus.Published, cancellationToken);
        if (!ownsAssessment)
        {
            return Forbid();
        }

        opportunity.LinkAssessment(assessmentId);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { organisationId, opportunityId });
    }

    [HttpPost("{opportunityId:guid}/unlink-assessment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkAssessment(Guid organisationId, Guid opportunityId, CancellationToken cancellationToken)
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

        opportunity.UnlinkAssessment();
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

        var wasAlreadyPublished = opportunity.Status == OpportunityStatus.Published;
        opportunity.Publish();
        if (!wasAlreadyPublished)
        {
            await BroadcastNewOpportunityAsync(opportunity, cancellationToken);
        }

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
            .Include(o => o.EligibilityRules)
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
        var candidateProfiles = await db.CandidateProfiles.AsNoTracking()
            .Where(p => candidateIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
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
        var passportIdsByCandidate = await db.SkillsPassports.AsNoTracking()
            .Where(p => candidateIds.Contains(p.CandidateId))
            .ToDictionaryAsync(p => p.CandidateId, p => p.Id, cancellationToken);

        var items = applications.Select(a =>
        {
            var identifiers = JsonSerializer.Deserialize<List<string>>(a.SharedCredentialsJson) ?? [];
            var attached = identifiers
                .Select(id => (CredentialIdentifier: id, CompetencyName: credentialsByIdentifier.TryGetValue(id, out var verificationRecordId)
                    && verificationRecordsByCompetency.TryGetValue(verificationRecordId, out var competencyId)
                    && competencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)"))
                .ToList();

            // Same public, read-only certificate page her own QR code opens - never a direct view of her
            // repositories, recordings, transcripts or scores (CLAUDE.md rule 9).
            string? certificateUrl = null;
            if (passportIdsByCandidate.TryGetValue(a.CandidateId, out var passportId))
            {
                var token = PassportCertificateToken.Generate(passportId, configuration);
                certificateUrl = Url.Action(nameof(PassportVerificationController.Certificate), "PassportVerification", new { passportId, token });
            }

            // Only meaningful when the opportunity actually has eligibility rules (typically a
            // bursary) - an empty list here means nothing to show, not "not eligible".
            var eligibility = candidateProfiles.TryGetValue(a.CandidateId, out var profile)
                ? OpportunityEligibilitySummary.Evaluate(opportunity, CandidateEligibilityData.FromProfile(profile))
                : null;

            return new ApplicationReviewItem
            {
                Application = a,
                CandidateName = candidateNames.GetValueOrDefault(a.CandidateId, "(unknown)"),
                AttachedCredentials = attached,
                PassportCertificateUrl = certificateUrl,
                Eligibility = eligibility,
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

        var row = await db.Applications
            .Join(db.Opportunities, a => a.OpportunityId, o => o.Id, (a, o) => new { Application = a, o.OrganisationId, o.Title })
            .Where(x => x.Application.Id == applicationId && x.Application.OpportunityId == opportunityId && x.OrganisationId == organisationId)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return NotFound();
        }

        row.Application.MarkReviewed();
        db.Add(new Notification(row.Application.CandidateId, NotificationType.RecruiterInterest,
            $"An employer reviewed your application for {row.Title}.", relatedCompetencyId: null, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Applications), new { organisationId, opportunityId });
    }

    /// <summary>So nobody misses a new job, internship, learnership or bursary: every candidate gets a
    /// notification the moment one goes live, not just the ones already watching the Opportunities page.</summary>
    private async Task BroadcastNewOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken)
    {
        var candidateIds = await db.CandidateProfiles.AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var message = $"New {opportunity.OpportunityType}: {opportunity.Title} was just posted.";
        foreach (var candidateId in candidateIds)
        {
            db.Add(new Notification(candidateId, NotificationType.NewOpportunity, message, relatedCompetencyId: null, now));
        }
    }
}
