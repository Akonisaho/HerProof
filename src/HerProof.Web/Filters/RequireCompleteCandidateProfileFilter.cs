using System.Security.Claims;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Filters;

/// <summary>A logged-in Candidate with an incomplete profile is redirected to /my-profile before
/// reaching the decorated action — "how do they browse without setting their profile?" shouldn't be
/// possible. Anonymous visitors are unaffected (IsInRole is false when unauthenticated), so public
/// opportunity browsing stays open as a marketing funnel; only authenticated candidates are gated.</summary>
public class RequireCompleteCandidateProfileFilter(HerProofDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.IsInRole(nameof(UserRole.Candidate)))
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var profile = await db.CandidateProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, context.HttpContext.RequestAborted);

            if (profile is not null && !profile.IsComplete)
            {
                context.Result = new Microsoft.AspNetCore.Mvc.RedirectResult("/my-profile?incomplete=1");
                return;
            }
        }

        await next();
    }
}
