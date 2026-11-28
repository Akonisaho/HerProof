using System.Security.Claims;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.ViewComponents;

/// <summary>The navbar's "Your opportunities" link for a Recruiter or Partner — resolves their
/// organisation so the link can carry its id, the same organisation HomeController.Index redirects
/// them to. Renders nothing if the signed-in user has no organisation membership yet.</summary>
public class OrganisationHomeLinkViewComponent(HerProofDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = Guid.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var organisationId = await db.OrganisationMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.OrganisationId)
            .FirstOrDefaultAsync();

        return View(organisationId);
    }
}
