using System.Security.Claims;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>docs/05 P16: candidate-reported progress against a recommended learning resource, and
/// reassessment requests for a competency that came back Developing or NotDemonstrated. Neither
/// action verifies anything by itself — "request reassessment" only re-queues the competency for an
/// assessor the same way submitting new evidence always has (CandidateCompetency.AddEvidence),
/// reusing the existing evidence-before-verification pipeline instead of adding a side door.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("learning")]
public class LearningController(HerProofDbContext db) : Controller
{
    [HttpPost("resources/{learningResourceId:guid}/progress")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetProgress(Guid learningResourceId, LearningProgressStatus status, string? returnUrl, CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);

        var progress = await db.LearningProgresses
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId && p.LearningResourceId == learningResourceId, cancellationToken);

        if (progress is null)
        {
            progress = new LearningProgress(candidateId, learningResourceId, DateTime.UtcNow);
            db.Add(progress);
        }

        progress.SetStatus(status, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/opportunities");
    }

    [HttpPost("competencies/{competencyId:guid}/request-reassessment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReassessment(Guid competencyId, string? returnUrl, CancellationToken cancellationToken)
    {
        var candidateId = await CurrentCandidateIdAsync(cancellationToken);

        var candidateCompetency = await db.CandidateCompetencies
            .FirstOrDefaultAsync(cc => cc.CandidateId == candidateId && cc.CompetencyId == competencyId, cancellationToken);

        if (candidateCompetency is null
            || candidateCompetency.Status is not (CandidateCompetencyStatus.Developing or CandidateCompetencyStatus.NotDemonstrated))
        {
            return BadRequest("Reassessment can only be requested for a competency that was already assessed as Developing or Not demonstrated.");
        }

        var evidence = candidateCompetency.AddEvidence("ReassessmentRequest", competencyId.ToString(), "Candidate requested reassessment.", DateTime.UtcNow);
        db.Add(evidence);
        await db.SaveChangesAsync(cancellationToken);

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/opportunities");
    }

    private async Task<Guid> CurrentCandidateIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId, cancellationToken);
        return profile.Id;
    }
}
