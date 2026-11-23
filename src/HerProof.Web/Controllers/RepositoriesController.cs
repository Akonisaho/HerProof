using System.Security.Claims;
using HerProof.Application.Repositories;
using HerProof.Domain.Identity;
using HerProof.Domain.Repositories;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>GitHub repository submission (docs/05 P07-P08): public repo URL only for now — private
/// repo access via GitHub App/OAuth is STUB(HP-011). Resolves to a pinned commit SHA (ADR-06) and
/// enqueues a RepositoryAnalysis row; HerProof.AssessmentWorker's background service claims it
/// (`FOR UPDATE SKIP LOCKED`, ADR-02) and runs the real pipeline (redact -> Roslyn -> LLM -> verify),
/// storing recommendations only — nothing here can ever set Verified.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("repositories")]
[EnableRateLimiting("upload")]
public class RepositoriesController(HerProofDbContext db, IGitHubClient gitHubClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var repositories = await db.Repositories.AsNoTracking()
            .Where(r => r.CandidateId == candidateId)
            .OrderByDescending(r => r.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var repositoryIds = repositories.Select(r => r.Id).ToList();
        var latestAnalysisIds = await db.RepositoryAnalyses.AsNoTracking()
            .Where(a => repositoryIds.Contains(a.RepositoryId))
            .GroupBy(a => a.RepositoryId)
            .Select(g => g.OrderByDescending(a => a.CreatedAtUtc).Select(a => a.Id).First())
            .ToListAsync(cancellationToken);

        var latestAnalyses = await db.RepositoryAnalyses.AsNoTracking()
            .Include(a => a.Findings)
            .Where(a => latestAnalysisIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        ViewBag.LatestAnalysisByRepository = latestAnalyses.ToDictionary(a => a.RepositoryId);
        ViewBag.PublishedOpportunities = await db.Opportunities.AsNoTracking()
            .Where(o => o.Status == Domain.Opportunities.OpportunityStatus.Published)
            .ToListAsync(cancellationToken);
        return View(repositories);
    }

    [HttpGet("submit")]
    public IActionResult Submit() => View(new SubmitRepositoryViewModel());

    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitRepositoryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var parsed = GitHubRepositoryUrlParser.TryParse(model.RepositoryUrl);
        if (parsed is null)
        {
            ModelState.AddModelError(nameof(model.RepositoryUrl), "Enter a valid public GitHub repository URL, e.g. https://github.com/owner/repo.");
            return View(model);
        }

        var candidateProfile = await GetCandidateProfileAsync(cancellationToken);
        if (candidateProfile is null)
        {
            return NotFound();
        }

        var candidateId = candidateProfile.Id;
        var (owner, repoName) = parsed.Value;
        string commitSha;
        try
        {
            commitSha = await gitHubClient.ResolveBranchToCommitShaAsync(owner, repoName, model.Branch, cancellationToken);
        }
        catch (GitHubNotFoundException)
        {
            ModelState.AddModelError(nameof(model.RepositoryUrl), "That repository or branch could not be found. Is it public?");
            return View(model);
        }
        catch (GitHubRateLimitException ex)
        {
            ModelState.AddModelError(string.Empty, $"GitHub is rate-limiting us right now. Try again in about {ex.RetryAfter.TotalMinutes:F0} minute(s).");
            return View(model);
        }

        var repository = new Repository(
            candidateId, repoName, model.RepositoryUrl, model.Branch, commitSha, RepositoryVisibility.Public, DateTime.UtcNow);

        // Flags, never blocks (CLAUDE.md rule 5): GitHub's contributor list can miss a real
        // contributor (private commit email, squashed PRs), so a failed or inconclusive check is
        // evidence for the assessor to see, not a reason to refuse the submission.
        var candidateUsername = GitHubContributorVerifier.ExtractUsername(candidateProfile.GitHubProfile);
        if (candidateUsername is not null)
        {
            try
            {
                var contributors = await gitHubClient.GetContributorLoginsAsync(owner, repoName, cancellationToken);
                repository.SetContributorStatus(GitHubContributorVerifier.Verify(candidateUsername, contributors));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // GitHub error (rate limit, 404 on an edge-case repo, etc.) — leave it NotChecked
                // rather than fail the whole submission over a secondary check.
            }
        }

        db.Repositories.Add(repository);

        // Enqueued as Pending; HerProof.AssessmentWorker's background service claims and processes it
        // (docs/01 section 6: "202 Accepted with analysis id ... poll or receive notification").
        var analysis = new RepositoryAnalysis(repository.Id, DateTime.UtcNow);
        db.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);

        TempData["AnalysisMessage"] = "Repository submitted. Analysis is queued — refresh this page in a few seconds to see progress.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Guid?> GetCandidateProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }

    private async Task<CandidateProfile?> GetCandidateProfileAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }
}
