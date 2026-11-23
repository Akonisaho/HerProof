using HerProof.Application.Passports;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Passports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>The public verify page (docs/05 P15): anonymous, read-only, and built exclusively from a
/// PassportShare's VisibleFields allow-list. CLAUDE.md rule 9 (privacy by default) means this
/// controller must never query recordings, transcripts, repositories, contact details, integrity
/// events or scores — not "hide them in the view", never fetch them here at all.</summary>
[AllowAnonymous]
[Route("verify")]
public class PassportVerificationController(HerProofDbContext db) : Controller
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Show(string token, CancellationToken cancellationToken)
    {
        var passports = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .Include(p => p.Shares)
            .ToListAsync(cancellationToken);

        foreach (var passport in passports)
        {
            var share = PassportShareService.Resolve(passport.Shares, token, DateTime.UtcNow);
            if (share is null)
            {
                continue;
            }

            var candidateUser = await db.Users.AsNoTracking()
                .Join(db.CandidateProfiles.AsNoTracking(), u => u.Id, p => p.UserId, (u, p) => new { u.FullName, ProfileId = p.Id })
                .FirstAsync(x => x.ProfileId == passport.CandidateId, cancellationToken);

            var competencyNames = await db.Competencies.AsNoTracking()
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
            var verificationRecordsByCompetency = await db.VerificationRecords.AsNoTracking()
                .Where(v => v.CandidateId == passport.CandidateId)
                .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);

            var visible = share.VisibleFields.ToHashSet();
            var credentials = passport.Credentials
                .Where(c => visible.Contains(c.CredentialIdentifier))
                .Select(c => new PublicCredentialViewModel
                {
                    CompetencyName = verificationRecordsByCompetency.TryGetValue(c.VerificationRecordId, out var competencyId)
                        && competencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)",
                    CredentialIdentifier = c.CredentialIdentifier,
                    IssuedAtUtc = c.IssuedAtUtc,
                    ValidUntilUtc = c.ValidUntilUtc,
                    Status = c.CredentialStatus.ToString(),
                })
                .ToList();

            var viewModel = new PublicPassportViewModel
            {
                PassportNumber = passport.PassportNumber,
                CandidateName = candidateUser.FullName,
                Credentials = credentials,
            };

            return View(viewModel);
        }

        return NotFound();
    }
}
