using System.Diagnostics;
using System.Security.Claims;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models;
using HerProof.Web.Models.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HerProofDbContext db;

    public HomeController(ILogger<HomeController> logger, HerProofDbContext db)
    {
        _logger = logger;
        this.db = db;
    }

    /// <summary>Logged-out visitors (and every non-Candidate role, for now) see the marketing page
    /// unchanged. A signed-in Candidate sees her own dashboard instead — real data only, honest empty
    /// states where she has none yet, never a fabricated "demo" look standing in for her own account.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.IsInRole(nameof(UserRole.Candidate)))
        {
            return View();
        }

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);
        var candidateProfile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (candidateProfile is null)
        {
            return View();
        }

        var passport = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .Include(p => p.Shares)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateProfile.Id, cancellationToken);

        var competencyNames = await db.Competencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var verificationRecordsByCompetency = passport is null
            ? new Dictionary<Guid, Guid>()
            : await db.VerificationRecords.AsNoTracking()
                .Where(v => v.CandidateId == candidateProfile.Id)
                .ToDictionaryAsync(v => v.Id, v => v.CompetencyId, cancellationToken);
        var finalLevelsByVerificationRecord = passport is null
            ? new Dictionary<Guid, int?>()
            : await db.VerificationRecords.AsNoTracking()
                .Where(v => v.CandidateId == candidateProfile.Id)
                .ToDictionaryAsync(v => v.Id, v => v.FinalLevel, cancellationToken);

        var verifiedCredentials = (passport?.Credentials ?? [])
            .Where(c => c.CredentialStatus == Domain.Passports.CredentialStatus.Active)
            .Select(c => (
                CompetencyName: verificationRecordsByCompetency.TryGetValue(c.VerificationRecordId, out var competencyId)
                    && competencyNames.TryGetValue(competencyId, out var name) ? name : "(unknown)",
                Level: finalLevelsByVerificationRecord.GetValueOrDefault(c.VerificationRecordId)))
            .ToList();

        var unverifiedCount = await db.CandidateCompetencies.AsNoTracking()
            .CountAsync(cc => cc.CandidateId == candidateProfile.Id
                && cc.Status != Domain.Competencies.CandidateCompetencyStatus.Verified, cancellationToken);

        var applicationCount = await db.Applications.AsNoTracking()
            .CountAsync(a => a.CandidateId == candidateProfile.Id, cancellationToken);

        var activeShare = passport?.Shares.FirstOrDefault(s => !s.IsRevoked && !s.IsExpired(DateTime.UtcNow));

        var recommendedOpportunities = await db.Opportunities.AsNoTracking()
            .Where(o => o.Status == Domain.Opportunities.OpportunityStatus.Published)
            .OrderBy(o => o.DeadlineUtc)
            .Take(3)
            .ToListAsync(cancellationToken);
        var organisationIds = recommendedOpportunities.Select(o => o.OrganisationId).Distinct().ToList();
        var organisationNames = await db.Organisations.AsNoTracking()
            .Where(o => organisationIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        var recentInterviews = await db.Interviews.AsNoTracking()
            .Where(i => i.CandidateId == candidateProfile.Id)
            .OrderByDescending(i => i.StartedAtUtc)
            .Take(2)
            .ToListAsync(cancellationToken);

        var model = new CandidateDashboardViewModel
        {
            FullName = user.FullName,
            PassportNumber = passport?.PassportNumber,
            VerifiedCredentials = verifiedCredentials,
            UnverifiedCompetencyCount = unverifiedCount,
            ApplicationCount = applicationCount,
            HasActiveShare = activeShare is not null,
            RecommendedOpportunities = recommendedOpportunities,
            OrganisationNamesByOpportunityId = organisationNames,
            RecentInterviews = recentInterviews,
        };

        return View("CandidateDashboard", model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>A real, scannable QR code for the homepage's credential mockup — it genuinely opens
    /// this live app, the same way a real Skills Passport share link's QR code does (PassportController
    /// has the real one). Not a static image: generated the same way every time, with the same library.</summary>
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult BrandQr()
    {
        var url = Url.Action(nameof(Index), "Home", null, Request.Scheme)!;
        return File(QrCodeGenerator.GeneratePng(url), "image/png");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [Route("Home/StatusCode/{code:int}")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }
}
