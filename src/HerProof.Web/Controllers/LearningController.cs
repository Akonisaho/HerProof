using System.Security.Claims;
using HerProof.Domain.Assessments;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Learning;
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
    /// <summary>"Not sure where to start?" - a candidate with no opportunity in mind yet picks a goal
    /// and gets a real, ordered skill roadmap with genuine external links (Microsoft Learn,
    /// freeCodeCamp, Codewars, GitHub docs) resolved from the same LearningResource data the
    /// opportunity gap report already uses - never an invented URL.</summary>
    [HttpGet("roadmap")]
    public async Task<IActionResult> Roadmap(string? goal, string? search, CancellationToken cancellationToken)
    {
        // "software developer" should match the goal "Software Developer", but so should a whole typed
        // sentence like "I want to become a software developer" - match in both directions, and also
        // accept every word of the goal name appearing somewhere in what she typed, in any order.
        var matchingGoals = string.IsNullOrWhiteSpace(search)
            ? CareerRoadmaps.Goals.Keys.ToList()
            : CareerRoadmaps.Goals.Keys.Where(g =>
                g.Contains(search, StringComparison.OrdinalIgnoreCase)
                || search.Contains(g, StringComparison.OrdinalIgnoreCase)
                || g.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries)
                    .All(word => search.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        if (goal is null || !CareerRoadmaps.Goals.TryGetValue(goal, out var goalDefinition))
        {
            return View(new CareerRoadmapViewModel { AvailableGoals = matchingGoals, SelectedGoal = null, Steps = null });
        }

        var (goalLanguage, steps) = goalDefinition;

        var candidateId = await CurrentCandidateIdAsync(cancellationToken);
        var competencyNames = steps.Select(s => s.CompetencyName).ToList();
        var competencies = await db.Competencies.AsNoTracking()
            .Where(c => competencyNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, cancellationToken);

        var competencyIds = competencies.Values.Select(c => c.Id).ToList();
        var candidateCompetencies = await db.CandidateCompetencies.AsNoTracking()
            .Where(cc => cc.CandidateId == candidateId && competencyIds.Contains(cc.CompetencyId))
            .ToDictionaryAsync(cc => cc.CompetencyId, cancellationToken);

        var learningResources = await db.LearningResources.AsNoTracking()
            .Where(lr => competencyIds.Contains(lr.CompetencyId))
            .ToListAsync(cancellationToken);

        // Published assessments that cover each of this roadmap's competencies - the same join
        // OpportunitiesController.Details already uses to match assessments to required competencies.
        // A competency like Problem-solving is genuinely covered by more than one assessment in
        // different languages (Core C# Practical AND Core Java Practical both tag it) - without
        // filtering by the goal's own language, a Java-only roadmap would wrongly offer the C#
        // practical too, and vice versa. Goals with no single language (goalLanguage is null) show
        // every real match, same as before.
        var matchingAssessments = await db.AssessmentCompetencies.AsNoTracking()
            .Where(ac => competencyIds.Contains(ac.CompetencyId))
            .Join(db.Assessments.AsNoTracking().Where(a => a.Status == AssessmentStatus.Published),
                ac => ac.AssessmentId, a => a.Id,
                (ac, a) => new { ac.CompetencyId, a.Id, a.Title, a.DurationMinutes, a.Language })
            .Where(a => goalLanguage == null || a.Language == goalLanguage)
            .ToListAsync(cancellationToken);
        var assessmentsByCompetencyId = matchingAssessments
            .GroupBy(a => a.CompetencyId)
            .ToDictionary(g => g.Key, g => g.Select(a => (a.Id, a.Title, a.DurationMinutes)).ToList());

        // Auto-detect a just-passed submission and record completion the moment she comes back here -
        // no separate "grade me" button, this piggybacks on the assessment flow that already exists
        // end to end. Never a human in this loop: RoadmapStepEvaluator reads the sandbox's own
        // automated result only.
        var existingCompletions = await db.RoadmapStepCompletions.AsNoTracking()
            .Where(c => c.CandidateId == candidateId && c.Goal == goal)
            .ToDictionaryAsync(c => c.CompetencyId, cancellationToken);

        var justCompletedCompetencyNames = new List<string>();
        foreach (var (competencyName, targetLevel) in steps)
        {
            if (!competencies.TryGetValue(competencyName, out var competency) || existingCompletions.ContainsKey(competency.Id))
            {
                continue;
            }

            // Path 1: a human Assessor already verified this competency - from any evidence at all
            // (repository, interview, a practical assessment, or several together), not only a
            // passing automated sandbox run. The strongest real evidence this app has should never
            // leave a roadmap step stuck locked just because she never happened to take the exact
            // assessment this roadmap would have offered her.
            if (candidateCompetencies.TryGetValue(competency.Id, out var cc) && cc.Status == CandidateCompetencyStatus.Verified)
            {
                var verifiedCompletion = new RoadmapStepCompletion(candidateId, goal, competency.Id, assessmentSessionId: null, verifiedByAssessor: true, DateTime.UtcNow);
                db.Add(verifiedCompletion);
                existingCompletions[competency.Id] = verifiedCompletion;
                justCompletedCompetencyNames.Add(competencyName);
                continue;
            }

            // Path 2: no human decision yet - fall back to the automated sandbox pass/fail this step's
            // own linked assessment produced, same as before.
            var stepAssessmentIds = matchingAssessments.Where(a => a.CompetencyId == competency.Id).Select(a => a.Id).ToList();
            if (stepAssessmentIds.Count == 0)
            {
                continue;
            }

            var passingSession = await db.AssessmentSessions.AsNoTracking()
                .Include(s => s.Submission)
                .Where(s => s.CandidateId == candidateId && stepAssessmentIds.Contains(s.AssessmentId) && s.Submission != null)
                .ToListAsync(cancellationToken);
            var passed = passingSession.FirstOrDefault(s => RoadmapStepEvaluator.Passed(s.Submission!.TestResultsJson));
            if (passed is not null)
            {
                var completion = new RoadmapStepCompletion(candidateId, goal, competency.Id, passed.Id, verifiedByAssessor: false, DateTime.UtcNow);
                db.Add(completion);
                existingCompletions[competency.Id] = completion;
                justCompletedCompetencyNames.Add(competencyName);
            }
        }

        if (justCompletedCompetencyNames.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var firstIncompleteIndex = steps
            .Select((s, i) => (s, i))
            .Where(x => competencies.TryGetValue(x.s.CompetencyName, out var c) && !existingCompletions.ContainsKey(c.Id))
            .Select(x => x.i)
            .DefaultIfEmpty(steps.Count)
            .First();

        // Several steps can legitimately share one assessment (Core C# Practical covers
        // Problem-solving, OOP and C# together) - for every step, name the OTHER roadmap
        // competencies that same assessment also covers, so it never reads as a dedicated test for
        // a skill it only tests incidentally.
        var roadmapCompetencyNamesById = steps
            .Where(s => competencies.ContainsKey(s.CompetencyName))
            .ToDictionary(s => competencies[s.CompetencyName].Id, s => s.CompetencyName);
        var otherCompetenciesByAssessmentId = matchingAssessments
            .GroupBy(a => a.Id)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => a.CompetencyId).Where(roadmapCompetencyNamesById.ContainsKey).Distinct().ToList());

        var resolvedSteps = steps
            .Select((s, i) => (Step: s, Index: i))
            .Where(x => competencies.ContainsKey(x.Step.CompetencyName))
            .Select(x =>
            {
                var competency = competencies[x.Step.CompetencyName];
                var resources = learningResources
                    .Where(lr => lr.CompetencyId == competency.Id && lr.TargetLevel <= x.Step.TargetLevel)
                    .OrderByDescending(lr => lr.TargetLevel)
                    .Select(lr => (lr.Title, lr.Url, lr.CostType))
                    .ToList();

                var availableAssessments = assessmentsByCompetencyId.GetValueOrDefault(competency.Id, [])
                    .Select(a =>
                    {
                        var alsoCovers = otherCompetenciesByAssessmentId.GetValueOrDefault(a.Id, [])
                            .Where(id => id != competency.Id)
                            .Select(id => roadmapCompetencyNamesById[id])
                            .ToList();
                        return (a.Id, a.Title, a.DurationMinutes, (IReadOnlyList<string>)alsoCovers);
                    })
                    .ToList();

                return new CareerRoadmapStep
                {
                    StepNumber = x.Index + 1,
                    CompetencyId = competency.Id,
                    CompetencyName = x.Step.CompetencyName,
                    TargetLevel = x.Step.TargetLevel,
                    CurrentStatus = candidateCompetencies.TryGetValue(competency.Id, out var cc) ? cc.Status : null,
                    Resources = resources,
                    IsCompleted = existingCompletions.ContainsKey(competency.Id),
                    IsLocked = x.Index > firstIncompleteIndex,
                    AvailableAssessments = availableAssessments,
                };
            })
            .ToList();

        return View(new CareerRoadmapViewModel
        {
            AvailableGoals = matchingGoals,
            SelectedGoal = goal,
            Steps = resolvedSteps,
            JustCompletedCompetencyNames = justCompletedCompetencyNames,
            GoalLanguage = goalLanguage,
        });
    }

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
