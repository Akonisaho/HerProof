using System.Security.Claims;
using HerProof.Application.Repositories;
using HerProof.Domain.Identity;
using HerProof.Domain.Repositories;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Filters;
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
[TypeFilter(typeof(RequireCompleteCandidateProfileFilter))]
public class RepositoriesController(HerProofDbContext db, IGitHubClient gitHubClient, IConfiguration configuration) : Controller
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

    /// <summary>Polled by repositories-poll.js so the page can show a live spinner and swap it for the
    /// real result in place, instead of asking the candidate to keep hitting refresh manually.</summary>
    [HttpGet("{repositoryId:guid}/status")]
    public async Task<IActionResult> Status(Guid repositoryId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var repository = await db.Repositories.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.CandidateId == candidateId, cancellationToken);
        if (repository is null)
        {
            return NotFound();
        }

        var latestAnalysis = await db.RepositoryAnalyses.AsNoTracking()
            .Where(a => a.RepositoryId == repositoryId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Json(new { status = latestAnalysis?.Status.ToString() ?? "Pending" });
    }

    [HttpGet("submit")]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken)
    {
        var candidateProfile = await GetCandidateProfileAsync(cancellationToken);
        ViewBag.MissingGitHubProfile = string.IsNullOrWhiteSpace(candidateProfile?.GitHubProfile);
        return View(new SubmitRepositoryViewModel());
    }

    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitRepositoryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Analysis genuinely runs code in a Docker sandbox (STUB(HP-038)) - a hosted preview with no
        // Docker daemon would queue this and never process it, leaving her repository stuck "Pending"
        // forever with no explanation. Refuse honestly up front instead.
        if (!configuration.GetValue("Execution:Enabled", true))
        {
            ModelState.AddModelError(string.Empty, "Repository analysis needs a sandbox that only runs in our local demo environment - this hosted preview is read-only for that part. Everything else here (gap reports, Evidence Packs, Skills Passports) is real.");
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
        string branch;
        try
        {
            (commitSha, branch) = await ResolveCommitShaWithBranchFallbackAsync(owner, repoName, model.Branch, cancellationToken);
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
        catch (GitHubApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            ModelState.AddModelError(nameof(model.Branch), "That branch name doesn't look valid. Double-check it and try again.");
            return View(model);
        }
        catch (GitHubApiException)
        {
            ModelState.AddModelError(string.Empty, "GitHub couldn't process that request right now. Please try again shortly.");
            return View(model);
        }

        var repository = new Repository(
            candidateId, repoName, model.RepositoryUrl, branch, commitSha, RepositoryVisibility.Public, DateTime.UtcNow);

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

        TempData["AnalysisMessage"] = "Repository submitted - analysis is running now.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tries the candidate-supplied branch first; if it fails, looks up the repo's real
    /// default branch and retries once before giving up. Handles the common case where the branch
    /// field was left at its "main" placeholder on a repo whose default branch is "master" (or
    /// anything else) — without this, a perfectly real, public repo reads as "not found" or "invalid
    /// branch name". GitHub is inconsistent about which status code a nonexistent ref gets on the
    /// commits/{ref} endpoint — sometimes 404 (GitHubNotFoundException), sometimes 422
    /// Unprocessable Entity (GitHubApiException) — so both trigger the same fallback.</summary>
    private async Task<(string CommitSha, string Branch)> ResolveCommitShaWithBranchFallbackAsync(
        string owner, string repoName, string requestedBranch, CancellationToken cancellationToken)
    {
        try
        {
            var sha = await gitHubClient.ResolveBranchToCommitShaAsync(owner, repoName, requestedBranch, cancellationToken);
            return (sha, requestedBranch);
        }
        catch (Exception ex) when (ex is GitHubNotFoundException
            || (ex is GitHubApiException apiEx && apiEx.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity))
        {
            var defaultBranch = await gitHubClient.GetDefaultBranchAsync(owner, repoName, cancellationToken);
            var sha = await gitHubClient.ResolveBranchToCommitShaAsync(owner, repoName, defaultBranch, cancellationToken);
            return (sha, defaultBranch);
        }
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
