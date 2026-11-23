using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Audit;
using HerProof.Infrastructure.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>POPIA data-subject self-service (CLAUDE.md rule 9, docs/05 P17): export and delete, for
/// the signed-in user's own account only — there is no admin "export anyone" variant. Deletion
/// anonymises identifying fields rather than deleting rows outright, because verification, review
/// and audit records are append-only by design (CLAUDE.md rule 8) and must survive; the right of
/// erasure is honoured at the level of "stop identifying this person", not "destroy the record that
/// something was verified".</summary>
[Authorize]
[Route("privacy")]
public class DataSubjectController(HerProofDbContext db, UserManager<ApplicationUser> userManager, IAuditLogger auditLogger) : Controller
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new() { WriteIndented = true };

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        object export = new
        {
            account = new { user.Email, user.FullName, user.CreatedAtUtc },
            candidateProfile = profile is null
                ? null
                : new
                {
                    profile.Province,
                    profile.EducationLevel,
                    profile.GenderDeclared,
                    profile.HouseholdIncomeBand,
                    profile.CareerInterests,
                    profile.GitHubProfile,
                    profile.LinkedInProfile,
                    profile.FieldOfStudy,
                    profile.DateOfBirth,
                },
            candidateCompetencies = profile is null
                ? []
                : await db.CandidateCompetencies.AsNoTracking()
                    .Where(cc => cc.CandidateId == profile.Id)
                    .Select(cc => new { cc.CompetencyId, cc.DeclaredLevel, cc.VerifiedLevel, Status = cc.Status.ToString() })
                    .ToListAsync(cancellationToken),
            applications = profile is null
                ? []
                : await db.Applications.AsNoTracking()
                    .Where(a => a.CandidateId == profile.Id)
                    .Select(a => new { a.OpportunityId, Status = a.Status.ToString(), a.SubmittedAtUtc })
                    .ToListAsync(cancellationToken),
            passport = profile is null
                ? null
                : await db.SkillsPassports.AsNoTracking()
                    .Where(p => p.CandidateId == profile.Id)
                    .Select(p => new { p.PassportNumber, p.Status })
                    .FirstOrDefaultAsync(cancellationToken),
        };

        await auditLogger.LogAsync(userId, "DataExportRequested", "ApplicationUser", userId, "{}", cancellationToken);

        var json = JsonSerializer.Serialize(export, ExportJsonOptions);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "herproof-my-data.json");
    }

    [HttpGet("delete")]
    public IActionResult ConfirmDelete() => View();

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        // Scrub identifying fields; the account row itself, and anything that references it
        // (verification records, audit log, applications), is append-only history and stays.
        var anonymousEmail = $"deleted-{userId:N}@herproof.invalid";
        user.FullName = "Deleted user";
        user.Email = anonymousEmail;
        user.UserName = anonymousEmail;
        user.NormalizedEmail = anonymousEmail.ToUpperInvariant();
        user.NormalizedUserName = anonymousEmail.ToUpperInvariant();
        user.IsActive = false;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        profile?.UpdateProfile(null, null, null, null, null, null, null, null, null);

        await auditLogger.LogAsync(userId, "DataDeletionRequested", "ApplicationUser", userId, "{}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return SignOut("Identity.Application");
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
