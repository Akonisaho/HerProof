using System.Security.Claims;
using HerProof.Application.Authorization;
using HerProof.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Infrastructure.Authorization;

public class ResourceAuthorizer(HerProofDbContext db) : IResourceAuthorizer
{
    public async Task<bool> CanAccessCandidateProfileAsync(ClaimsPrincipal user, Guid candidateProfileId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return false;
        }

        if (user.IsInRole(nameof(Domain.Identity.UserRole.Administrator)))
        {
            return true;
        }

        var profile = await db.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == candidateProfileId, cancellationToken);

        return profile is not null && profile.UserId == userId;
    }

    public async Task<bool> CanAccessOrganisationAsync(ClaimsPrincipal user, Guid organisationId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return false;
        }

        if (user.IsInRole(nameof(Domain.Identity.UserRole.Administrator)))
        {
            return true;
        }

        return await db.OrganisationMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganisationId == organisationId && m.UserId == userId, cancellationToken);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
