using System.Security.Claims;
using HerProof.Application.Authorization;
using HerProof.Domain.Opportunities;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>The one landing page a Recruiter or Partner actually lands on (docs/05 P04/P16 gap): before
/// this, signing in dropped them straight into their opportunity list with no overview of what's
/// actually pending - same gap the Assessor inbox closed for human reviewers. Creates nothing; every
/// number here is a real read of data that already exists elsewhere (Opportunities, Applications,
/// CandidateOutreaches) - this just assembles it into one place.</summary>
[Authorize(Roles = "Recruiter,Partner")]
[Route("organisations/{organisationId:guid}/dashboard")]
public class OrganisationDashboardController(HerProofDbContext db, IResourceAuthorizer resourceAuthorizer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid organisationId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessOrganisationAsync(User, organisationId, cancellationToken))
        {
            return Forbid();
        }

        var organisation = await db.Organisations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return NotFound();
        }

        var opportunities = await db.Opportunities.AsNoTracking()
            .Where(o => o.OrganisationId == organisationId)
            .ToListAsync(cancellationToken);
        var opportunityIds = opportunities.Select(o => o.Id).ToList();

        var applications = await db.Applications.AsNoTracking()
            .Where(a => opportunityIds.Contains(a.OpportunityId))
            .ToListAsync(cancellationToken);

        var pendingByOpportunity = applications
            .Where(a => a.Status == CandidateApplicationStatus.Submitted)
            .GroupBy(a => a.OpportunityId)
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = opportunities
            .OrderByDescending(o => pendingByOpportunity.GetValueOrDefault(o.Id))
            .ThenByDescending(o => o.DeadlineUtc)
            .Select(o => new OrganisationDashboardOpportunityRow
            {
                OpportunityId = o.Id,
                Title = o.Title,
                OpportunityType = o.OpportunityType.ToString(),
                Status = o.Status.ToString(),
                TotalApplications = applications.Count(a => a.OpportunityId == o.Id),
                PendingApplications = pendingByOpportunity.GetValueOrDefault(o.Id),
            })
            .ToList();

        var recruiterUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reachedOutCandidateCount = await db.CandidateOutreaches.AsNoTracking()
            .Where(o => o.RecruiterUserId == recruiterUserId)
            .Select(o => o.CandidateId)
            .Distinct()
            .CountAsync(cancellationToken);

        var viewModel = new OrganisationDashboardViewModel
        {
            OrganisationId = organisationId,
            OrganisationName = organisation.Name,
            IsBursaryPartner = User.IsInRole("Partner"),
            PublishedCount = opportunities.Count(o => o.Status == OpportunityStatus.Published),
            TotalOpportunityCount = opportunities.Count,
            TotalPendingApplications = pendingByOpportunity.Values.Sum(),
            ReachedOutCandidateCount = reachedOutCandidateCount,
            Opportunities = rows,
        };

        return View(viewModel);
    }
}
