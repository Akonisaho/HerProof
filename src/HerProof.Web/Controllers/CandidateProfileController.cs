using System.Security.Claims;
using HerProof.Application.Authorization;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Authorization;
using HerProof.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Resource-ownership check (CLAUDE.md P03): a Candidate may read or edit only her own
/// profile, never another candidate's, regardless of role.</summary>
[Authorize(Policy = Policies.RequireCandidate)]
[Route("candidates/{candidateProfileId:guid}/profile")]
public class CandidateProfileController(HerProofDbContext db, IResourceAuthorizer resourceAuthorizer) : Controller
{
    /// <summary>Nav-bar convenience: "My profile" doesn't need to know its own id up front.</summary>
    [HttpGet("/my-profile")]
    public async Task<IActionResult> MyProfile(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null ? NotFound() : RedirectToAction(nameof(Index), new { candidateProfileId = profile.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid candidateProfileId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessCandidateProfileAsync(User, candidateProfileId, cancellationToken))
        {
            return Forbid();
        }

        var profile = await db.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == candidateProfileId, cancellationToken);

        return profile is null ? NotFound() : View(profile);
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit(Guid candidateProfileId, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessCandidateProfileAsync(User, candidateProfileId, cancellationToken))
        {
            return Forbid();
        }

        var profile = await db.CandidateProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == candidateProfileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        return View(new EditProfileViewModel
        {
            Province = profile.Province,
            EducationLevel = profile.EducationLevel,
            GenderDeclared = profile.GenderDeclared,
            HouseholdIncomeBand = profile.HouseholdIncomeBand,
            CareerInterests = profile.CareerInterests,
            GitHubProfile = profile.GitHubProfile,
            LinkedInProfile = profile.LinkedInProfile,
            FieldOfStudy = profile.FieldOfStudy,
            DateOfBirth = profile.DateOfBirth,
        });
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid candidateProfileId, EditProfileViewModel model, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorizer.CanAccessCandidateProfileAsync(User, candidateProfileId, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == candidateProfileId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        profile.UpdateProfile(
            model.Province, model.EducationLevel, model.GenderDeclared, model.HouseholdIncomeBand,
            model.CareerInterests, model.GitHubProfile, model.LinkedInProfile, model.FieldOfStudy, model.DateOfBirth);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index), new { candidateProfileId });
    }
}
