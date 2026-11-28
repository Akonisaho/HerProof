using HerProof.Application.Passports;
using HerProof.Domain.Passports;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Passports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HerProof.Web.Controllers;

/// <summary>The public verify page (docs/05 P15): anonymous, read-only, and built exclusively from a
/// PassportShare's VisibleFields allow-list. CLAUDE.md rule 9 (privacy by default) means this
/// controller must never query recordings, transcripts, repositories, contact details, integrity
/// events or scores — not "hide them in the view", never fetch them here at all.</summary>
[AllowAnonymous]
[Route("verify")]
public class PassportVerificationController(HerProofDbContext db, IConfiguration configuration) : Controller
{
    /// <summary>The target of a passport's always-on "general certificate" QR (docs/05 P15): unlike a
    /// PassportShare, this isn't a one-time, candidate-selected reveal - it always shows whichever
    /// credentials are Active right now, computed live on every scan, never frozen at creation time.</summary>
    [HttpGet("certificate/{passportId:guid}")]
    public async Task<IActionResult> Certificate(Guid passportId, string token, CancellationToken cancellationToken)
    {
        if (!PassportCertificateToken.IsValid(passportId, token, configuration))
        {
            return NotFound();
        }

        var passport = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .FirstOrDefaultAsync(p => p.Id == passportId, cancellationToken);
        if (passport is null)
        {
            return NotFound();
        }

        var candidateUser = await db.Users.AsNoTracking()
            .Join(db.CandidateProfiles.AsNoTracking(), u => u.Id, p => p.UserId, (u, p) => new { u.FullName, ProfileId = p.Id })
            .FirstAsync(x => x.ProfileId == passport.CandidateId, cancellationToken);

        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var verificationRecordsByCompetency = await db.VerificationRecords.AsNoTracking()
            .Where(v => v.CandidateId == passport.CandidateId)
            .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);

        var credentials = passport.Credentials
            .Where(c => c.CredentialStatus == CredentialStatus.Active)
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

        return View(new PublicPassportViewModel
        {
            PassportNumber = passport.PassportNumber,
            CandidateName = candidateUser.FullName,
            Credentials = credentials,
        });
    }


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
