using System.Security.Claims;
using HerProof.AI;
using HerProof.Application.Interviews;
using HerProof.Application.Repositories;
using HerProof.Application.Speech;
using HerProof.Domain.Competencies;
using HerProof.Domain.Consent;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Opportunities;
using HerProof.Domain.Repositories;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Filters;
using HerProof.Web.Models.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HerProof.Web.Controllers;

/// <summary>Nolwazi (docs/05 P10/P11, docs/06): text-first rubric and question/probe engine, with a
/// voice layer (consent screen, browser mic capture, server-side STT/TTS) sitting on top of the exact
/// same evaluation path — a confirmed transcript is a confirmed transcript regardless of how it got
/// typed into the box.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("interviews")]
[EnableRateLimiting("interview")]
[TypeFilter(typeof(RequireCompleteCandidateProfileFilter))]
public class InterviewController(
    HerProofDbContext db,
    RepositoryContentFetcher contentFetcher,
    NolwaziInterviewEngine thandiEngine,
    ISpeechToText speechToText,
    ITextToSpeech textToSpeech,
    IConfiguration configuration,
    ILogger<InterviewController> logger) : Controller
{
    private const string VoiceConsentPurpose = "voice-interview";
    private const string VoiceConsentVersion = "1.0";

    /// <summary>HP-022, closed: previously a completed/in-progress interview was only reachable from
    /// the repositories page's "Start interview" form right after creating it — there was nowhere to
    /// come back and find it again.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return View(Array.Empty<Interview>());
        }

        var interviews = await db.Interviews.AsNoTracking()
            .Where(i => i.CandidateId == candidateId)
            .OrderByDescending(i => i.StartedAtUtc)
            .ToListAsync(cancellationToken);

        var opportunityIds = interviews.Where(i => i.OpportunityId is not null).Select(i => i.OpportunityId!.Value).Distinct().ToList();
        ViewBag.OpportunityTitles = await db.Opportunities.AsNoTracking()
            .Where(o => opportunityIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Title, cancellationToken);

        return View(interviews);
    }

    [HttpGet("consent")]
    public IActionResult Consent(Guid repositoryId, Guid? opportunityId) =>
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
    public Task<IActionResult> Start(Guid repositoryId, Guid? opportunityId, CancellationToken cancellationToken) =>
        StartInterviewAsync(repositoryId, opportunityId, InterviewMode.Text, cancellationToken);

    private async Task<IActionResult> StartInterviewAsync(Guid repositoryId, Guid? opportunityId, InterviewMode mode, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var repository = await db.Repositories.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == repositoryId && r.CandidateId == candidateId, cancellationToken);
        if (repository is null)
        {
            return NotFound();
        }

        Opportunity? opportunity = null;
        if (opportunityId is not null)
        {
            opportunity = await db.Opportunities.AsNoTracking()
                .Include(o => o.RequiredCompetencies)
                .FirstOrDefaultAsync(o => o.Id == opportunityId, cancellationToken);
            if (opportunity is null)
            {
                return NotFound();
            }
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

        // No opportunity: prove whatever the repository itself demonstrates (docs/05 — evidence
        // before verification shouldn't require a posting to exist first), driven by the repo's own
        // detected competencies rather than one opportunity's required list.
        var requiredCompetencyNames = opportunity is not null
            ? opportunity.RequiredCompetencies
                .Select(rc => competencyNames.GetValueOrDefault(rc.CompetencyId, "Unknown"))
                .Distinct()
                .ToList()
            : persistedFindings
                .Select(f => competencyNames.GetValueOrDefault(f.CompetencyId, "Unknown"))
                .Distinct()
                .ToList();

        var parsed = GitHubRepositoryUrlParser.TryParse(repository.RepositoryUrl);
        if (parsed is null)
        {
            return BadRequest("Repository URL could not be parsed.");
        }

        // Only the files the already-stored findings actually reference — the broad tree-wide fetch
        // belongs to the original P08 analysis pass, not to starting an interview on top of it (that
        // was re-fetching and re-selecting from the whole repo every time, ~40+ GitHub calls and up to
        // 90s for a modest repo).
        var findingPaths = persistedFindings.Select(f => f.FilePath).Distinct().ToList();

        FetchedRepositoryContent content;
        try
        {
            content = findingPaths.Count > 0
                ? await contentFetcher.FetchSelectedAsync(parsed.Value.Owner, parsed.Value.Repo, repository.CommitSha, findingPaths, cancellationToken)
                : await contentFetcher.FetchAsync(parsed.Value.Owner, parsed.Value.Repo, repository.CommitSha, cancellationToken);
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
        catch (GitHubApiException)
        {
            TempData["InterviewMessage"] = "GitHub couldn't process that request right now. Please try again shortly.";
            return RedirectToAction(nameof(RepositoriesController.Index), "Repositories");
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException or HttpRequestException)
        {
            // A plain network timeout/drop isn't any of the typed GitHub exceptions above, but it's
            // just as transient — never let it surface as an unhandled 500 to the candidate.
            logger.LogWarning(ex, "Transient network failure fetching repository content for interview start.");
            TempData["InterviewMessage"] = "GitHub didn't respond in time. Please try again in a moment.";
            return RedirectToAction(nameof(RepositoriesController.Index), "Repositories");
        }

        var questions = await thandiEngine.GenerateQuestionsAsync(staticFindings, [], requiredCompetencyNames, content, cancellationToken);

        if (questions.Count == 0)
        {
            TempData["InterviewMessage"] = "Nolwazi couldn't generate questions from this repository yet. Try again shortly.";
            return RedirectToAction(nameof(RepositoriesController.Index), "Repositories");
        }

        var competencyIdsByName = competencyNames.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);
        var interviewerName = configuration["Interviewer:Name"] ?? "Nolwazi";
        var interview = new Interview(candidateId.Value, repositoryId, opportunityId, mode, interviewerName, DateTime.UtcNow);

        var order = 0;
        foreach (var q in questions)
        {
            if (!competencyIdsByName.TryGetValue(q.CompetencyName, out var competencyId))
            {
                // Confirmed live: a model can still invent a competencyName that isn't a real one
                // (e.g. "Architecture" instead of a real competency from the required list) despite
                // the prompt now telling it not to - this used to drop the question with no trace at
                // all, silently shrinking the interview to fewer questions than were actually generated.
                logger.LogWarning(
                    "Nolwazi question dropped at interview start: competencyName {CompetencyName} does not match any real competency.",
                    q.CompetencyName);
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
            await EnsureEvidenceSubmittedAsync(interview, cancellationToken);
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
        // Named explicitly, not a bare View(model): Answer() calls this method directly (not through
        // the MVC action pipeline) when ModelState is invalid, and a bare View() resolves against the
        // ALREADY-EXECUTING action's name ("Answer"), not this method's name - confirmed live, this
        // crashed every single time with "The view 'Answer' was not found" whenever an answer failed
        // validation (e.g. a blank submission), which looked like "submit does nothing" to the candidate.
        return View("Take", new SubmitAnswerViewModel { TurnId = turn!.Id });
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
        var promptTurn = question?.Turns.FirstOrDefault(t => t.Id == model.TurnId);
        if (question is null || promptTurn is null)
        {
            return NotFound();
        }

        // Append-only (CLAUDE.md rule 8): promptTurn was already persisted in Take and may never be
        // mutated, so the answer is recorded on a brand-new turn instead of editing it in place.
        // Text mode: typed input is both the raw and confirmed transcript (docs/06 section 9 point 7 —
        // no STT, so there is nothing to correct).
        var turn = question.RecordAnswerTurn(promptTurn, model.Answer, recordingReference: null, durationSeconds: 0, DateTime.UtcNow);
        turn.ConfirmTranscript(model.Answer);
        db.Add(turn);

        var repository = await db.Repositories.AsNoTracking().FirstAsync(r => r.Id == interview.RepositoryId, cancellationToken);
        var parsed = GitHubRepositoryUrlParser.TryParse(repository.RepositoryUrl)!.Value;

        FetchedRepositoryContent content;
        try
        {
            content = await contentFetcher.FetchAsync(parsed.Owner, parsed.Repo, repository.CommitSha, cancellationToken);
        }
        catch (GitHubRateLimitException ex)
        {
            // Confirmed live: leaving this turn unevaluated here (the previous behaviour) traps the
            // candidate in an unbreakable loop - Take() re-shows this same unevaluated turn's copied
            // prompt text as if it were the current question (with no visible error, since
            // TempData["InterviewMessage"] is only ever rendered on the Repositories page, not here),
            // she resubmits, GitHub is still rate-limited, and it repeats forever with no way to ever
            // reach a probe or finish the interview. Evaluate it as Partial instead - same fail-safe
            // pattern as a null AI result below - so the interview always keeps moving.
            turn.Evaluate(Verdict.Partial, $"Could not be verified against the repository right now (GitHub rate limit, retry in about {ex.RetryAfter.TotalMinutes:F0} minute(s)); recorded as Partial for assessor review.", "{}");
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Take), new { interviewId });
        }
        catch (Exception ex) when (ex is GitHubNotFoundException or GitHubApiException)
        {
            turn.Evaluate(Verdict.Partial, "Could not be verified against the repository right now (GitHub was unreachable); recorded as Partial for assessor review.", "{}");
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Take), new { interviewId });
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException or HttpRequestException)
        {
            // Same reasoning as the typed GitHub exceptions above, for a plain network drop/timeout
            // that isn't wrapped in one of them (see StartInterviewAsync's matching catch).
            logger.LogWarning(ex, "Transient network failure fetching repository content for interview answer evaluation.");
            turn.Evaluate(Verdict.Partial, "Could not be verified against the repository right now (network timeout); recorded as Partial for assessor review.", "{}");
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Take), new { interviewId });
        }

        var probeCount = question.ProbePromptCount();
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

    /// <summary>An interview is one sitting, not a draft: there is no "resume later". The Take page
    /// sends this via navigator.sendBeacon on tab close/navigation-away, and the candidate list page
    /// never links back into an in-progress interview, so once left, it's left for good.</summary>
    [HttpPost("{interviewId:guid}/abandon")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abandon(Guid interviewId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        if (interview.Status == InterviewStatus.InProgress)
        {
            interview.Abandon();
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok();
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

    /// <summary>Nolwazi's spoken prompt, synthesised server-side (ADR-05: the key never reaches the
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
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
        ViewBag.CompetencyNames = competencyNames;
        ViewBag.CandidateName = user.FullName;
        return View(interview);
    }

    [HttpGet("{interviewId:guid}/report.pdf")]
    public async Task<IActionResult> ReportPdf(Guid interviewId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        var interview = await LoadInterviewAsync(interviewId, candidateId, cancellationToken);
        if (interview is null)
        {
            return NotFound();
        }

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
        var competencyNames = await db.Competencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        string? verifyUrl = null;
        if (interview.Status == InterviewStatus.Completed)
        {
            var token = InterviewCertificateToken.Generate(interview.Id, configuration);
            verifyUrl = Url.Action(nameof(InterviewCertificateController.Show), "InterviewCertificate", new { interviewId = interview.Id, token }, Request.Scheme);
        }

        var pdfBytes = InterviewReportPdfExporter.Export(user.FullName, interview, competencyNames, verifyUrl);
        return File(pdfBytes, "application/pdf", $"interview-report-{interview.StartedAtUtc:yyyy-MM-dd}.pdf");
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

    /// <summary>The missing link that makes "evidence before verification" actually reachable: until
    /// this ran, finishing an interview left no CandidateCompetency row behind at all, so a candidate
    /// could complete a full interview and still never appear in the Assessor inbox (AssessorDashboard
    /// only lists EvidenceSubmitted/UnderAssessment/AssessmentCompleted rows). One row per competency
    /// the interview actually covered, evidenced by this interview's id.</summary>
    private async Task EnsureEvidenceSubmittedAsync(Interview interview, CancellationToken cancellationToken)
    {
        var competencyIds = interview.Questions.Select(q => q.CompetencyId).Distinct().ToList();
        var existing = await db.CandidateCompetencies
            .Where(cc => cc.CandidateId == interview.CandidateId && competencyIds.Contains(cc.CompetencyId))
            .ToDictionaryAsync(cc => cc.CompetencyId, cancellationToken);

        foreach (var competencyId in competencyIds)
        {
            if (!existing.TryGetValue(competencyId, out var candidateCompetency))
            {
                candidateCompetency = new CandidateCompetency(interview.CandidateId, competencyId, declaredLevel: null, DateTime.UtcNow);
                db.CandidateCompetencies.Add(candidateCompetency);
            }

            // Confirmed live: this is the exact pre-existing EF gotcha this codebase's own STUBS.md
            // already warns about (P04 note) - a child added via domain-model collection navigation
            // onto an ALREADY-TRACKED parent gets mistracked as Modified rather than Added, and since
            // CompetencyEvidence is append-only, AppendOnlyInterceptor then (correctly) blocks what it
            // sees as an illegitimate UPDATE - which surfaced as a genuine DbUpdateConcurrencyException
            // ("expected 1 row(s), affected 0") crashing interview completion for any competency that
            // already had a CandidateCompetency row from an earlier interview. db.Add here, matching
            // the same fix already applied at LearningController.RequestReassessment.
            var evidence = candidateCompetency.AddEvidence("Interview", interview.Id.ToString(), $"Nolwazi interview completed {DateTime.UtcNow:u}.", DateTime.UtcNow);
            db.Add(evidence);
        }
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
