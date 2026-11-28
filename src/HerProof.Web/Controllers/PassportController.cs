using System.Security.Claims;
using HerProof.Application.Passports;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Passports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HerProof.Web.Controllers;

/// <summary>A candidate's own Skills Passport (docs/05 P15). Only ever shows this signed-in
/// candidate's own passport — there is no route parameter to swap identities, unlike the
/// evidence-pack pattern in P14, because a passport has exactly one owner.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("passport")]
public class PassportController(HerProofDbContext db, IConfiguration configuration) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);

        var passport = await db.SkillsPassports
            .AsNoTracking()
            .Include(p => p.Credentials)
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);

        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var verificationRecordsByCompetency = passport is null
            ? []
            : await db.VerificationRecords.AsNoTracking()
                .Where(v => v.CandidateId == candidateId)
                .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);
        ViewBag.FinalLevelByVerificationRecordId = passport is null
            ? new Dictionary<Guid, int?>()
            : await db.VerificationRecords.AsNoTracking()
                .Where(v => v.CandidateId == candidateId)
                .ToDictionaryAsync(v => v.Id, v => v.FinalLevel, cancellationToken);

        var verifiedCredentials = passport?.Credentials
            .Select(c => (Credential: c, CompetencyName: verificationRecordsByCompetency.TryGetValue(c.VerificationRecordId, out var competencyId)
                && competencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)"))
            .ToList() ?? [];

        var candidateCompetencies = await db.CandidateCompetencies.AsNoTracking()
            .Where(cc => cc.CandidateId == candidateId
                && cc.Status != Domain.Competencies.CandidateCompetencyStatus.Verified)
            .ToListAsync(cancellationToken);
        var unverified = candidateCompetencies
            .Select(cc => (CandidateCompetency: cc, CompetencyName: competencyNames.GetValueOrDefault(cc.CompetencyId, "(unknown)")))
            .ToList();

        var viewModel = new PassportDashboardViewModel
        {
            Passport = passport,
            VerifiedCredentials = verifiedCredentials,
            UnverifiedCompetencies = unverified,
            ActiveShares = passport?.Shares.Where(s => !s.IsRevoked && !s.IsExpired(DateTime.UtcNow)).ToList() ?? [],
            RevokedOrExpiredShares = passport?.Shares.Where(s => s.IsRevoked || s.IsExpired(DateTime.UtcNow)).ToList() ?? [],
        };

        return View(viewModel);
    }

    [HttpPost("shares")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateShare(CreateShareViewModel model, CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);
        var passport = await db.SkillsPassports
            .Include(p => p.Credentials)
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);

        if (passport is null || model.SelectedCredentialIdentifiers.Count == 0)
        {
            TempData["Error"] = "Choose at least one verified credential to share.";
            return RedirectToAction(nameof(Index));
        }

        DateTime? expiresAtUtc = model.ExpiresInDays is > 0
            ? DateTime.UtcNow.AddDays(model.ExpiresInDays.Value)
            : null;

        var (share, rawToken) = PassportShareService.CreateShare(passport, model.SelectedCredentialIdentifiers, DateTime.UtcNow, expiresAtUtc);
        db.Add(share);
        await db.SaveChangesAsync(cancellationToken);

        var verifyUrl = Url.Action(nameof(PassportVerificationController.Show), "PassportVerification", new { token = rawToken }, Request.Scheme)!;
        var qrPngBytes = QrCodeGenerator.GeneratePng(verifyUrl);

        TempData["ShareVerifyUrl"] = verifyUrl;
        TempData["ShareQrBase64"] = Convert.ToBase64String(qrPngBytes);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("shares/{shareId:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeShare(Guid shareId, CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);
        var passport = await db.SkillsPassports
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);

        var share = passport?.Shares.FirstOrDefault(s => s.Id == shareId);
        if (share is null)
        {
            return NotFound();
        }

        share.Revoke(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("shares/{shareId:guid}/qr.png")]
    public async Task<IActionResult> ShareQrCode(Guid shareId, string token, CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);
        var passport = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);
        var share = passport?.Shares.FirstOrDefault(s => s.Id == shareId);
        if (share is null || PassportShareService.Resolve([share], token, DateTime.UtcNow) is null)
        {
            return NotFound();
        }

        var url = Url.Action(nameof(PassportVerificationController.Show), "PassportVerification", new { token }, Request.Scheme)!;
        return File(QrCodeGenerator.GeneratePng(url), "image/png");
    }

    /// <summary>The always-on "general certificate" QR shown directly on this page - unlike
    /// ShareQrCode above, this never needs a share to be created first: it's always there, and always
    /// points at whatever is currently Active (see PassportVerificationController.Certificate).</summary>
    [HttpGet("certificate-qr.png")]
    public async Task<IActionResult> GeneralCertificateQr(CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);
        var passport = await db.SkillsPassports.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);
        if (passport is null)
        {
            return NotFound();
        }

        var token = PassportCertificateToken.Generate(passport.Id, configuration);
        var url = Url.Action(nameof(PassportVerificationController.Certificate), "PassportVerification", new { passportId = passport.Id, token }, Request.Scheme)!;
        return File(QrCodeGenerator.GeneratePng(url), "image/png");
    }

    [HttpGet("export.pdf")]
    public async Task<IActionResult> ExportPdf(CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == GetUserId(), cancellationToken);
        var passport = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);

        if (passport is null)
        {
            return NotFound();
        }

        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var verificationRecordsByCompetency = await db.VerificationRecords.AsNoTracking()
            .Where(v => v.CandidateId == candidateId)
            .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);

        var rows = passport.Credentials.Select(c => (
            CompetencyName: verificationRecordsByCompetency.TryGetValue(c.VerificationRecordId, out var competencyId)
                && competencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)",
            c.CredentialIdentifier,
            c.IssuedAtUtc,
            c.ValidUntilUtc,
            Status: c.CredentialStatus.ToString())).ToList();

        var token = PassportCertificateToken.Generate(passport.Id, configuration);
        var verifyUrl = Url.Action(nameof(PassportVerificationController.Certificate), "PassportVerification", new { passportId = passport.Id, token }, Request.Scheme);

        var pdfBytes = PassportPdfExporter.Export(user.FullName, passport.PassportNumber, rows, verifyUrl);
        return File(pdfBytes, "application/pdf", $"{passport.PassportNumber}-passport.pdf");
    }

    private async Task<Guid> CurrentCandidateIdAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var profile = await db.CandidateProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId, cancellationToken);
        return profile.Id;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
