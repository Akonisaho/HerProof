using HerProof.Domain.Interviews;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HerProof.Web.Controllers;

/// <summary>The public QR target printed on an interview report/certificate: anonymous, read-only,
/// and built from an explicit allow-list of fields - never transcripts, scores, verdicts or repository
/// details (CLAUDE.md rule 9, same model as PassportVerificationController).</summary>
[AllowAnonymous]
[Route("verify-interview")]
public class InterviewCertificateController(HerProofDbContext db, IConfiguration configuration) : Controller
{
    [HttpGet("{interviewId:guid}")]
    public async Task<IActionResult> Show(Guid interviewId, string token, CancellationToken cancellationToken)
    {
        if (!InterviewCertificateToken.IsValid(interviewId, token, configuration))
        {
            return NotFound();
        }

        var interview = await db.Interviews.AsNoTracking()
            .Include(i => i.Questions)
            .FirstOrDefaultAsync(i => i.Id == interviewId && i.Status == InterviewStatus.Completed, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        var candidateUser = await db.Users.AsNoTracking()
            .Join(db.CandidateProfiles.AsNoTracking(), u => u.Id, p => p.UserId, (u, p) => new { u.FullName, ProfileId = p.Id })
            .FirstAsync(x => x.ProfileId == interview.CandidateId, cancellationToken);

        var competencyIds = interview.Questions.Select(q => q.CompetencyId).Distinct().ToList();
        var competencyNames = await db.Competencies.AsNoTracking()
            .Where(c => competencyIds.Contains(c.Id))
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var viewModel = new PublicInterviewCertificateViewModel
        {
            CandidateName = candidateUser.FullName,
            InterviewerPersona = interview.InterviewerPersona,
            Mode = interview.Mode.ToString(),
            CompletedAtUtc = interview.CompletedAtUtc,
            CompetencyNames = competencyNames,
        };

        return View(viewModel);
    }

    [HttpGet("{interviewId:guid}/qr.png")]
    public async Task<IActionResult> Qr(Guid interviewId, CancellationToken cancellationToken)
    {
        var exists = await db.Interviews.AsNoTracking()
            .AnyAsync(i => i.Id == interviewId && i.Status == InterviewStatus.Completed, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var token = InterviewCertificateToken.Generate(interviewId, configuration);
        var url = Url.Action(nameof(Show), null, new { interviewId, token }, Request.Scheme)!;
        return File(QrCodeGenerator.GeneratePng(url), "image/png");
    }
}
