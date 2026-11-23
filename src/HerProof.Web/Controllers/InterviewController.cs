using System.Security.Claims;
using HerProof.AI;
using HerProof.Application.Interviews;
using HerProof.Application.Repositories;
using HerProof.Application.Speech;
using HerProof.Domain.Consent;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Repositories;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Thandi (docs/05 P10/P11, docs/06): text-first rubric and question/probe engine, with a
/// voice layer (consent screen, browser mic capture, server-side STT/TTS) sitting on top of the exact
/// same evaluation path — a confirmed transcript is a confirmed transcript regardless of how it got
/// typed into the box.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("interviews")]
[EnableRateLimiting("interview")]
public class InterviewController(
    HerProofDbContext db,
    RepositoryContentFetcher contentFetcher,
    ThandiInterviewEngine thandiEngine,
    ISpeechToText speechToText,
    ITextToSpeech textToSpeech) : Controller
{
    private const string VoiceConsentPurpose = "voice-interview";
    private const string VoiceConsentVersion = "1.0";

    [HttpGet("consent")]
    public IActionResult Consent(Guid repositoryId, Guid opportunityId) =>
        View(new VoiceConsentViewModel { RepositoryId = repositoryId, OpportunityId = opportunityId });

    [HttpPost("consent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Consent(VoiceConsentViewModel model, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.ConsentRecords.Add(new ConsentRecord(userId, VoiceConsentPurpose, VoiceConsentVersion, model.ConsentGiven, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        // Starts the interview directly rather than redirecting to a second antiforgery-protected
        // endpoint: a GET can't carry the token this POST already validated, and a state-changing
        // request (creating an Interview) shouldn't be a GET anyway.
        var mode = model.ConsentGiven ? InterviewMode.Voice : InterviewMode.Text;
        return await StartInterviewAsync(model.RepositoryId, model.OpportunityId, mode, cancellationToken);
    }

    [HttpPost("start")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Start(Guid repositoryId, Guid opportunityId, CancellationToken cancellationToken) =>
        StartInterviewAsync(repositoryId, opportunityId, InterviewMode.Text, cancellationToken);

    private async Task<IActionResult> StartInterviewAsync(Guid repositoryId, Guid opportunityId, InterviewMode mode, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var repository = await db.Repositories.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.CandidateId == candidateId, cancellationToken);
        var opportunity = await db.Opportunities.AsNoTracking()
            .Include(o => o.RequiredCompetencies)
            .FirstOrDefaultAsync(o => o.Id == opportunityId, cancellationToken);
        if (repository is null || opportunity is null)
        {
            return NotFound();
        }

        var persistedFindings = await db.RepositoryFindings.AsNoTracking()
            .Join(db.RepositoryAnalyses.AsNoTracking(), f => f.AnalysisId, a => a.Id, (f, a) => new { Finding = f, a.RepositoryId })
            .Where(x => x.RepositoryId == repositoryId)
            .Select(x => x.Finding)
            .ToListAsync(cancellationToken);

        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var staticFindings = persistedFindings
            .Select(f => new StaticFinding(competencyNames.GetValueOrDefault(f.CompetencyId, "Unknown"), f.FindingType, f.FilePath, f.LineReference, f.Description, f.Evidence))
            .ToList();

        var requiredCompetencyNames = opportunity.RequiredCompetencies
            .Select(rc => competencyNames.GetValueOrDefault(rc.CompetencyId, "Unknown"))
            .Distinct()
            .ToList();

        var parsed = GitHubRepositoryUrlParser.TryParse(repository.RepositoryUrl);
        if (parsed is null)
        {
            return BadRequest("Repository URL could not be parsed.");
        }

        FetchedRepositoryContent content;
        try
        {
            content = await contentFetcher.FetchAsync(parsed.Value.Owner, parsed.Value.Repo, repository.CommitSha, cancellationToken);
        }
        catch (GitHubRateLimitException ex)
        {
            TempData["InterviewMessage"] = $"GitHub is rate-limiting us right now. Try again in about {ex.RetryAfter.TotalMinutes:F0} minute(s).";
            return RedirectToAction(nameof(RepositoriesController.Index), "Repositories");
        }
        catch (GitHubNotFoundException)
        {
            TempData["InterviewMessage"] = "That repository could not be re-fetched from GitHub right now.";
            return RedirectToAction(nameof(RepositoriesController.Index), "Repositories");
        }

        var questions = await thandiEngine.GenerateQuestionsAsync(staticFindings, [], requiredCompetencyNames, content, cancellationToken);

        if (questions.Count == 0)
        {
            TempData["InterviewMessage"] = "Thandi couldn't generate questions from this repository yet. Try again shortly.";
            return RedirectToAction(nameof(RepositoriesController.Index), "Repositories");
        }

        var competencyIdsByName = competencyNames.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
        var interview = new Interview(candidateId.Value, repositoryId, opportunityId, mode, "Thandi", DateTime.UtcNow);

        var order = 0;
        foreach (var q in questions)
        {
            if (!competencyIdsByName.TryGetValue(q.CompetencyName, out var competencyId))
            {
                continue;
            }

            interview.AskQuestion(competencyId, q.Text, q.SnippetFilePath, q.SnippetLines, order++);
        }

        db.Interviews.Add(interview);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Take), new { interviewId = interview.Id });
    }

    [HttpGet("{interviewId:guid}")]
    public async Task<IActionResult> Take(Guid interviewId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        if (interview.Status == InterviewStatus.Completed)
        {
            return RedirectToAction(nameof(Report), new { interviewId });
        }

        var active = FindActiveQuestionAndTurn(interview, out var isNewQuestion);
        if (active is null)
        {
            interview.Complete(DateTime.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Report), new { interviewId });
        }

        var (question, existingTurn) = active.Value;
        var turn = existingTurn;
        if (isNewQuestion)
        {
            turn = question.AddTurn(TurnKind.Question, question.QuestionText, DateTime.UtcNow);
            db.Add(turn);
            await db.SaveChangesAsync(cancellationToken);
        }

        ViewBag.Question = question;
        ViewBag.Interview = interview;
        return View(new SubmitAnswerViewModel { TurnId = turn!.Id });
    }

    [HttpPost("{interviewId:guid}/answer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(Guid interviewId, SubmitAnswerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Take(interviewId, cancellationToken);
        }

        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        var question = interview.Questions.FirstOrDefault(q => q.Turns.Any(t => t.Id == model.TurnId));
        var turn = question?.Turns.FirstOrDefault(t => t.Id == model.TurnId);
        if (question is null || turn is null)
        {
            return NotFound();
        }

        // Text mode: typed input is both the raw and confirmed transcript (docs/06 section 9 point 7 —
        // no STT, so there is nothing to correct).
        turn.RecordAnswer(model.Answer, recordingReference: null, durationSeconds: 0);
        turn.ConfirmTranscript(model.Answer);

        var repository = await db.Repositories.AsNoTracking().FirstAsync(r => r.Id == interview.RepositoryId, cancellationToken);
        var parsed = GitHubRepositoryUrlParser.TryParse(repository.RepositoryUrl)!.Value;

        FetchedRepositoryContent content;
        try
        {
            content = await contentFetcher.FetchAsync(parsed.Owner, parsed.Repo, repository.CommitSha, cancellationToken);
        }
        catch (GitHubRateLimitException ex)
        {
            // Never lose the candidate's answer on a transient GitHub problem: it's already recorded
            // above. Let her retry the evaluation step shortly.
            TempData["InterviewMessage"] = $"GitHub is rate-limiting us right now. Your answer was saved — try again in about {ex.RetryAfter.TotalMinutes:F0} minute(s).";
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Take), new { interviewId });
        }

        var probeCount = question.Turns.Count(t => t.TurnKind == TurnKind.Probe);
        var snippetContent = content.Files.FirstOrDefault(f => f.Path == question.SnippetFilePath)?.Content ?? string.Empty;

        var result = await thandiEngine.EvaluateTurnAsync(
            question.QuestionText, question.SnippetFilePath ?? "", question.SnippetLines ?? "", snippetContent,
            model.Answer, probeCount, content, cancellationToken);

        if (result is null)
        {
            // Fail safe: never block the candidate on a bad model response. Treat as inconclusive and move on.
            turn.Evaluate(Verdict.Partial, "The evaluation could not be completed; recorded as Partial for assessor review.", "{}");
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Take), new { interviewId });
        }

        var verdict = ParseVerdict(result.VerdictSoFar);
        var evidenceJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            covered = result.CoveredPoints,
            missing = result.MissingPoints,
            contradictions = result.Contradictions,
        });
        turn.Evaluate(verdict, BuildEvidenceSummary(result), evidenceJson);

        if (result.NextAction is NextAction.Probe or NextAction.Clarify)
        {
            var kind = result.NextAction == NextAction.Probe ? TurnKind.Probe : TurnKind.Clarify;
            try
            {
                var nextTurn = question.AddTurn(kind, result.NextUtterance, DateTime.UtcNow);
                db.Add(nextTurn);
            }
            catch (InvalidOperationException)
            {
                // Probe cap already reached (defence in depth: the engine itself also enforces this) — move on.
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Take), new { interviewId });
    }

    [HttpPost("{interviewId:guid}/switch-to-text")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchToText(Guid interviewId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        interview.SwitchToText();
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Take), new { interviewId });
    }

    /// <summary>Browser records an answer, posts the raw audio here, gets back a transcript to review
    /// and correct before it's ever submitted (docs/05 P11: "transcript review and correction before
    /// scoring"). The audio itself is never written to disk or any store — it exists only for the
    /// length of this request, which is also why there's nothing to encrypt-at-rest here.</summary>
    [HttpPost("{interviewId:guid}/turns/{turnId:guid}/transcribe")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Transcribe(Guid interviewId, Guid turnId, IFormFile audio, CancellationToken cancellationToken)
    {
        if (audio.Length == 0 || audio.Length > 10 * 1024 * 1024)
        {
            return BadRequest("Audio answer must be between 1 byte and 10 MB.");
        }

        var allowedContentTypes = new[] { "audio/webm", "audio/ogg", "audio/wav", "audio/mp4", "audio/mpeg" };
        if (!allowedContentTypes.Contains(audio.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Unsupported audio format.");
        }

        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        var question = interview?.Questions.FirstOrDefault(q => q.Turns.Any(t => t.Id == turnId));
        if (interview is null || question is null)
        {
            return NotFound();
        }

        var findingEvidence = await db.RepositoryFindings.AsNoTracking()
            .Where(f => f.CompetencyId == question.CompetencyId)
            .Select(f => f.Evidence)
            .Take(10)
            .ToListAsync(cancellationToken);
        var phraseHints = PhraseHintExtractor.Extract([question.SnippetFilePath, .. findingEvidence]);

        using var memoryStream = new MemoryStream();
        await audio.CopyToAsync(memoryStream, cancellationToken);

        var result = await speechToText.TranscribeAsync(memoryStream.ToArray(), phraseHints, cancellationToken);
        return Json(new { transcript = result.Text });
    }

    /// <summary>Thandi's spoken prompt, synthesised server-side (ADR-05: the key never reaches the
    /// browser) and cached by CachingTextToSpeech. 204 (no audio) tells the browser to fall back to
    /// its own built-in speech synthesis instead.</summary>
    [HttpGet("{interviewId:guid}/turns/{turnId:guid}/prompt-audio")]
    public async Task<IActionResult> PromptAudio(Guid interviewId, Guid turnId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        var turn = interview?.Questions.SelectMany(q => q.Turns).FirstOrDefault(t => t.Id == turnId);
        if (interview is null || turn is null)
        {
            return NotFound();
        }

        var speech = await textToSpeech.SynthesizeAsync(turn.PromptText, cancellationToken);
        if (speech.AudioBytes.Length == 0)
        {
            return NoContent();
        }

        return File(speech.AudioBytes, speech.ContentType);
    }

    [HttpGet("{interviewId:guid}/report")]
    public async Task<IActionResult> Report(Guid interviewId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        var competencyNames = await db.Competencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        ViewBag.CompetencyNames = competencyNames;
        return View(interview);
    }

    private static (InterviewQuestion Question, InterviewTurn? Turn)? FindActiveQuestionAndTurn(Interview interview, out bool isNewQuestion)
    {
        foreach (var question in interview.Questions.OrderBy(q => q.QuestionOrder))
        {
            if (question.Turns.Count == 0)
            {
                isNewQuestion = true;
                return (question, null);
            }

            var lastTurn = question.Turns.OrderBy(t => t.TurnIndex).Last();
            if (lastTurn.Evaluation is null)
            {
                isNewQuestion = false;
                return (question, lastTurn);
            }

            // Last turn is evaluated and no further turn was added for it: the probe ladder finished
            // (MoveOn/FinishQuestion) and this question is done. Fall through to the next question.
        }

        isNewQuestion = false;
        return null;
    }

    private static Verdict ParseVerdict(string verdict) => verdict switch
    {
        "Understood" => Verdict.Understood,
        "NotDemonstrated" => Verdict.NotDemonstrated,
        _ => Verdict.Partial,
    };

    private static string BuildEvidenceSummary(TurnEvaluationResult result)
    {
        var parts = new List<string>();
        if (result.CoveredPoints.Count > 0)
        {
            parts.Add($"Covered: {string.Join(", ", result.CoveredPoints)}.");
        }

        if (result.MissingPoints.Count > 0)
        {
            parts.Add($"Not yet covered: {string.Join(", ", result.MissingPoints)}.");
        }

        foreach (var c in result.Contradictions)
        {
            parts.Add($"Candidate said \"{c.CandidateSaid}\" but {c.File}:{c.Lines} shows {c.CodeShows}. Not evidence of misconduct on its own. Assessor review required.");
        }

        return parts.Count > 0 ? string.Join(' ', parts) : "No specific evidence recorded.";
    }

    private async Task<Interview?> LoadInterviewAsync(Guid interviewId, Guid? candidateId, CancellationToken cancellationToken)
    {
        if (candidateId is null)
        {
            return null;
        }

        return await db.Interviews
            .Include(i => i.Questions).ThenInclude(q => q.Turns).ThenInclude(t => t.Evaluation)
            .FirstOrDefaultAsync(i => i.Id == interviewId && i.CandidateId == candidateId, cancellationToken);
    }

    private async Task<Guid?> GetCandidateProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }
}
