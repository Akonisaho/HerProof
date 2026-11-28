using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using HerProof.Application.Assessments;
using HerProof.Application.Audit;
using HerProof.Application.Passports;
using HerProof.Application.Verification;
using HerProof.Domain.Assessments;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Notifications;
using HerProof.Domain.Verification;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Assessments;
using HerProof.Web.Models.Verification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Evidence before verification (CLAUDE.md rule 1, docs/00 differentiator #1): this is the
/// only place in the whole codebase a VerificationRecord can be created, and only via
/// VerificationDecisionService, which rejects anyone not in role Assessor regardless of what this
/// controller's own [Authorize] does — defence in depth, tested at both layers.</summary>
[Authorize(Roles = nameof(UserRole.Assessor))]
[Route("assessor")]
public class AssessorDashboardController(HerProofDbContext db, IAuditLogger auditLogger) : Controller
{
    /// <summary>docs/STUBS.md: no email provider the demo can rely on, so "a candidate submitted
    /// evidence, go look" lives here instead of in an inbox — this list IS the inbox. An item counts
    /// as read once this assessor has opened its evidence pack since it last changed (tracked via the
    /// audit log, so no schema change was needed); unread items sort first.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var assessorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var pending = await db.CandidateCompetencies.AsNoTracking()
            .Where(cc => cc.Status == CandidateCompetencyStatus.EvidenceSubmitted
                || cc.Status == CandidateCompetencyStatus.UnderAssessment
                || cc.Status == CandidateCompetencyStatus.AssessmentCompleted)
            .ToListAsync(cancellationToken);

        var candidateIds = pending.Select(p => p.CandidateId).Distinct().ToList();
        var competencyIds = pending.Select(p => p.CompetencyId).Distinct().ToList();

        ViewBag.CandidateNames = await db.CandidateProfiles.AsNoTracking()
            .Join(db.Users.AsNoTracking(), p => p.UserId, u => u.Id, (p, u) => new { p.Id, u.FullName })
            .Where(x => candidateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        ViewBag.CompetencyNames = await db.Competencies.AsNoTracking()
            .Where(c => competencyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var viewedLog = await db.AuditLog.AsNoTracking()
            .Where(a => a.ActorUserId == assessorId && a.Action == "EvidencePackViewed" && candidateIds.Contains(a.EntityId))
            .ToListAsync(cancellationToken);

        var unreadKeys = new HashSet<string>();
        foreach (var cc in pending)
        {
            var wasViewedSinceUpdate = viewedLog.Any(a => a.EntityId == cc.CandidateId
                && a.DetailJson.Contains(cc.CompetencyId.ToString())
                && a.AtUtc >= cc.UpdatedAtUtc);
            if (!wasViewedSinceUpdate)
            {
                unreadKeys.Add($"{cc.CandidateId}:{cc.CompetencyId}");
            }
        }

        ViewBag.UnreadCount = unreadKeys.Count;

        // One card per candidate, however many competencies she has pending (previously one row per
        // competency, so "Anko" appeared four separate times) - same idiom as an opportunity card
        // listing once with its applicants underneath, not once per applicant.
        var candidateNames = (IReadOnlyDictionary<Guid, string>)ViewBag.CandidateNames;
        var competencyNames = (IReadOnlyDictionary<Guid, string>)ViewBag.CompetencyNames;

        var rows = pending
            .GroupBy(cc => cc.CandidateId)
            .Select(g => new InboxCandidateRow
            {
                CandidateId = g.Key,
                CandidateName = candidateNames.GetValueOrDefault(g.Key, "(unknown)"),
                PendingCount = g.Count(),
                UnreadCount = g.Count(cc => unreadKeys.Contains($"{cc.CandidateId}:{cc.CompetencyId}")),
                MostRecentUpdateUtc = g.Max(cc => cc.UpdatedAtUtc),
                CompetencyNames = g.Select(cc => competencyNames.GetValueOrDefault(cc.CompetencyId, "(unknown)")).ToList(),
            })
            .OrderByDescending(r => r.UnreadCount > 0)
            .ThenByDescending(r => r.MostRecentUpdateUtc)
            .ToList();

        return View(rows);
    }

    /// <summary>"All of her assessments in one folder" - every competency this candidate has ever
    /// touched, whatever its status, plus her issued Skills Passport credentials if any - one place
    /// to see the whole picture instead of hunting through separate inbox rows per competency.
    /// Read-only, creates nothing (same as the evidence pack it links out to).</summary>
    [HttpGet("candidates/{candidateId:guid}")]
    public async Task<IActionResult> CandidateOverview(Guid candidateId, CancellationToken cancellationToken)
    {
        var candidateProfile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == candidateId, cancellationToken);
        if (candidateProfile is null)
        {
            return NotFound();
        }

        var candidateUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == candidateProfile.UserId, cancellationToken);

        var candidateCompetencies = await db.CandidateCompetencies.AsNoTracking()
            .Where(cc => cc.CandidateId == candidateId)
            .ToListAsync(cancellationToken);
        var competencyNames = await db.Competencies.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var competencyRows = candidateCompetencies
            .OrderBy(cc => competencyNames.GetValueOrDefault(cc.CompetencyId, "(unknown)"))
            .Select(cc => new CandidateOverviewCompetencyRow
            {
                CompetencyId = cc.CompetencyId,
                CompetencyName = competencyNames.GetValueOrDefault(cc.CompetencyId, "(unknown)"),
                Status = cc.Status.ToString(),
                DeclaredLevel = cc.DeclaredLevel,
                VerifiedLevel = cc.VerifiedLevel,
            })
            .ToList();

        var passport = await db.SkillsPassports.AsNoTracking()
            .Include(p => p.Credentials)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);

        var credentialRows = new List<CandidateOverviewCredentialRow>();
        if (passport is not null)
        {
            var verificationRecordIds = passport.Credentials.Select(c => c.VerificationRecordId).ToList();
            var verificationRecords = await db.VerificationRecords.AsNoTracking()
                .Where(v => verificationRecordIds.Contains(v.Id))
                .ToListAsync(cancellationToken);

            credentialRows = passport.Credentials
                .Select(cred =>
                {
                    var record = verificationRecords.FirstOrDefault(v => v.Id == cred.VerificationRecordId);
                    return new CandidateOverviewCredentialRow
                    {
                        CompetencyName = record is null ? "(unknown)" : competencyNames.GetValueOrDefault(record.CompetencyId, "(unknown)"),
                        FinalLevel = record?.FinalLevel,
                        IssuedAtUtc = cred.IssuedAtUtc,
                        CredentialStatus = cred.CredentialStatus.ToString(),
                    };
                })
                .OrderBy(r => r.CompetencyName)
                .ToList();
        }

        var viewModel = new CandidateOverviewViewModel
        {
            CandidateId = candidateId,
            CandidateName = candidateUser.FullName,
            Competencies = competencyRows,
            PassportNumber = passport?.PassportNumber,
            Credentials = credentialRows,
        };

        return View(viewModel);
    }

    [HttpGet("candidates/{candidateId:guid}/competencies/{competencyId:guid}")]
    public async Task<IActionResult> EvidencePack(Guid candidateId, Guid competencyId, CancellationToken cancellationToken)
    {
        var candidateProfile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == candidateId, cancellationToken);
        var competency = await db.Competencies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competencyId, cancellationToken);
        if (candidateProfile is null || competency is null)
        {
            return NotFound();
        }

        var candidateUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == candidateProfile.UserId, cancellationToken);
        var candidateCompetency = await db.CandidateCompetencies.AsNoTracking()
            .FirstOrDefaultAsync(cc => cc.CandidateId == candidateId && cc.CompetencyId == competencyId, cancellationToken);

        var candidateRepositories = await db.Repositories.AsNoTracking()
            .Where(r => r.CandidateId == candidateId)
            .ToListAsync(cancellationToken);
        var candidateRepositoryIds = candidateRepositories.Select(r => r.Id).ToList();
        var candidateAnalyses = await db.RepositoryAnalyses.AsNoTracking()
            .Where(a => candidateRepositoryIds.Contains(a.RepositoryId))
            .ToListAsync(cancellationToken);
        var candidateAnalysisIds = candidateAnalyses.Select(a => a.Id).ToList();

        var findings = await db.RepositoryFindings.AsNoTracking()
            .Where(f => candidateAnalysisIds.Contains(f.AnalysisId) && f.CompetencyId == competencyId)
            .ToListAsync(cancellationToken);
        // Which actual repositories backed this competency's findings - the Evidence Pack previously
        // never named the repository itself, only individual file:line references buried inside it.
        var relevantRepositoryIds = findings
            .Select(f => candidateAnalyses.FirstOrDefault(a => a.Id == f.AnalysisId)?.RepositoryId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var relevantRepositories = candidateRepositories
            .Where(r => relevantRepositoryIds.Contains(r.Id))
            .Select(r => new RepositorySummaryRow
            {
                RepositoryName = r.RepositoryName,
                RepositoryUrl = r.RepositoryUrl,
                Branch = r.Branch,
                Visibility = r.Visibility.ToString(),
                ContributorStatus = r.ContributorStatus.ToString(),
            })
            .ToList();

        // The assessor previously saw only "src/Foo.cs:5-15" as plain text - no way to actually open
        // the real file at the exact commit the analysis ran against and read it in context. Each
        // finding came from one analysis of one repository, so build a real GitHub permalink per finding.
        var findingSourceLinks = findings.ToDictionary(
            f => f.Id,
            f =>
            {
                var analysis = candidateAnalyses.FirstOrDefault(a => a.Id == f.AnalysisId);
                var repository = analysis is null ? null : candidateRepositories.FirstOrDefault(r => r.Id == analysis.RepositoryId);
                return repository is null ? null : BuildGitHubFileLink(repository.RepositoryUrl, repository.CommitSha, f.FilePath, f.LineReference);
            });
        // ExecutionReport itself carries no timestamp; order by its parent analysis's completion
        // time (most recent first) so the Assessor sees her latest attempt front and centre, with
        // older ones tucked behind "Show earlier attempts" rather than a long undifferentiated list.
        var executionReports = await db.ExecutionReports.AsNoTracking()
            .Where(r => candidateAnalysisIds.Contains(r.AnalysisId))
            .ToListAsync(cancellationToken);
        executionReports = executionReports
            .OrderByDescending(r => candidateAnalyses.First(a => a.Id == r.AnalysisId).CompletedAtUtc)
            .ToList();

        var candidateInterviews = await db.Interviews.AsNoTracking()
            .Where(i => i.CandidateId == candidateId)
            .ToListAsync(cancellationToken);
        var interviewIds = candidateInterviews.Select(i => i.Id).ToList();
        var questions = await db.InterviewQuestions.AsNoTracking()
            .Where(q => interviewIds.Contains(q.InterviewId) && q.CompetencyId == competencyId)
            .ToListAsync(cancellationToken);
        var questionIds = questions.Select(q => q.Id).ToList();
        var turns = await db.InterviewTurns.AsNoTracking()
            .Include(t => t.Evaluation)
            .Where(t => questionIds.Contains(t.QuestionId) && t.Evaluation != null)
            .ToListAsync(cancellationToken);
        var interviewTurns = turns
            .Select(t => (Question: questions.First(q => q.Id == t.QuestionId), Turn: t))
            .ToList();
        // Same gap as the repository findings list: a question's snippet reference was plain text,
        // no way to actually open the file Nolwazi's question was about. Each question belongs to one
        // interview, which was asked against one repository at a known commit.
        var questionSourceLinks = questions.ToDictionary(
            q => q.Id,
            q =>
            {
                var interview = candidateInterviews.FirstOrDefault(i => i.Id == q.InterviewId);
                var repository = interview is null ? null : candidateRepositories.FirstOrDefault(r => r.Id == interview.RepositoryId);
                return repository is null || string.IsNullOrWhiteSpace(q.SnippetFilePath)
                    ? null
                    : BuildGitHubFileLink(repository.RepositoryUrl, repository.CommitSha, q.SnippetFilePath, q.SnippetLines ?? "");
            });

        var assessmentIdsForCompetency = await db.AssessmentCompetencies.AsNoTracking()
            .Where(ac => ac.CompetencyId == competencyId)
            .Select(ac => ac.AssessmentId)
            .ToListAsync(cancellationToken);
        var candidateSessionIds = await db.AssessmentSessions.AsNoTracking()
            .Where(s => s.CandidateId == candidateId && assessmentIdsForCompetency.Contains(s.AssessmentId))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var candidateSubmissions = await db.AssessmentSubmissions.AsNoTracking()
            .Where(s => candidateSessionIds.Contains(s.SessionId))
            .ToListAsync(cancellationToken);
        var submissionIds = candidateSubmissions.Select(s => s.Id).ToList();
        var assessmentReviews = await db.AssessmentReviews.AsNoTracking()
            .Where(r => submissionIds.Contains(r.SubmissionId))
            .ToListAsync(cancellationToken);
        var reviewedSubmissionIds = assessmentReviews.Select(r => r.SubmissionId).ToHashSet();
        // Each row carries its own timestamp and automated pass/fail so the list reads like
        // distinct submissions, not N identical unlabelled buttons.
        var pendingSubmissions = candidateSubmissions
            .Where(s => !reviewedSubmissionIds.Contains(s.Id))
            .OrderByDescending(s => s.SubmittedAtUtc)
            .Select(s => new PendingSubmissionRow
            {
                SubmissionId = s.Id,
                SubmittedAtUtc = s.SubmittedAtUtc,
                AutomatedSummary = SummarizeTestResults(s.TestResultsJson),
            })
            .ToList();
        var integrityTimeline = await db.IntegrityEvents.AsNoTracking()
            .Where(e => candidateSessionIds.Contains(e.SessionId))
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var decisionHistory = await db.VerificationRecords.AsNoTracking()
            .Where(v => v.CandidateId == candidateId && v.CompetencyId == competencyId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var aiSuggestion = AiRecommendationDeriver.Derive(
            interviewTurns.Select(t => t.Turn.Evaluation!.Verdict).ToList(),
            findings.Count > 0);

        var viewModel = new EvidencePackViewModel
        {
            CandidateName = candidateUser.FullName,
            CompetencyName = competency.Name,
            DeclaredLevel = candidateCompetency?.DeclaredLevel,
            VerifiedLevel = candidateCompetency?.VerifiedLevel,
            CandidateCompetencyStatus = candidateCompetency?.Status.ToString() ?? "NoEvidence",
            RepositoryFindings = findings,
            FindingSourceLinks = findingSourceLinks,
            RelevantRepositories = relevantRepositories,
            ExecutionReports = executionReports,
            PendingSubmissions = pendingSubmissions,
            InterviewTurns = interviewTurns,
            QuestionSourceLinks = questionSourceLinks,
            AssessmentReviews = assessmentReviews,
            IntegrityTimeline = integrityTimeline,
            DecisionHistory = decisionHistory,
            AiSuggestion = aiSuggestion,
        };

        ViewBag.CandidateId = candidateId;
        ViewBag.CompetencyId = competencyId;
        ViewBag.IsDemoData = candidateAnalyses.Any(a => a.IsDemoData) || candidateInterviews.Any(i => i.IsDemoData);

        var assessorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await auditLogger.LogAsync(assessorId, "EvidencePackViewed", "CandidateCompetency", candidateId,
            JsonSerializer.Serialize(new { competencyId }), cancellationToken);

        return View(viewModel);
    }

    /// <summary>Closes a real gap found live: the evidence pack showed a "Practical assessment
    /// reviews" section reading from AssessmentReview, but nothing anywhere ever created one - a
    /// human never actually saw the candidate's submitted code, only the automated sandbox pass/fail.
    /// This shows her real submitted file content, question by question, next to each question's
    /// hidden test (never shown to her) and the sandbox's own result, so the review below is grounded
    /// in what she actually wrote, not a summary of it.</summary>
    [HttpGet("submissions/{submissionId:guid}/review")]
    public async Task<IActionResult> ReviewSubmission(Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await db.AssessmentSubmissions.AsNoTracking()
            .Include(s => s.Review)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        var session = await db.AssessmentSessions.AsNoTracking()
            .FirstAsync(s => s.Id == submission.SessionId, cancellationToken);
        var assessment = await db.Assessments.AsNoTracking()
            .Include(a => a.Questions)
            .FirstAsync(a => a.Id == session.AssessmentId, cancellationToken);
        var candidateProfile = await db.CandidateProfiles.AsNoTracking()
            .FirstAsync(p => p.Id == session.CandidateId, cancellationToken);
        var candidateUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == candidateProfile.UserId, cancellationToken);

        var submittedFiles = JsonSerializer.Deserialize<List<SubmittedFile>>(submission.SubmissionReference) ?? [];
        var questions = assessment.Questions
            .OrderBy(q => q.Order)
            .Select(q => new SubmissionQuestionViewModel
            {
                Title = q.Title,
                Prompt = q.Prompt,
                FileName = q.FileName,
                HiddenTestCode = q.HiddenTestCode,
                SubmittedContent = submittedFiles.FirstOrDefault(f => f.Path == q.FileName)?.Content,
            })
            .ToList();

        var viewModel = new SubmissionReviewViewModel
        {
            SubmissionId = submission.Id,
            CandidateName = candidateUser.FullName,
            AssessmentTitle = assessment.Title,
            Questions = questions,
            TestResultsJson = submission.TestResultsJson,
            ExistingReview = submission.Review,
        };

        return View(viewModel);
    }

    [HttpPost("submissions/{submissionId:guid}/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(Guid submissionId, SubmitReviewViewModel model, CancellationToken cancellationToken)
    {
        var submission = await db.AssessmentSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        var assessorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAssessor = User.IsInRole(nameof(UserRole.Assessor));

        // The only AssessmentReview construction site in the codebase (see AssessmentReviewService).
        var review = AssessmentReviewService.Create(
            isAssessor, submission, assessorId, criterionScoresJson: "{}", model.Feedback, model.ReviewStatus, DateTime.UtcNow);
        db.Add(review);
        await db.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(assessorId, "AssessmentReviewed", "AssessmentSubmission", submissionId,
            JsonSerializer.Serialize(new { model.ReviewStatus }), cancellationToken);

        return RedirectToAction(nameof(ReviewSubmission), new { submissionId });
    }

    [HttpPost("candidates/{candidateId:guid}/competencies/{competencyId:guid}/decide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(Guid candidateId, Guid competencyId, DecideViewModel model, CancellationToken cancellationToken)
    {
        var assessorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAssessor = User.IsInRole(nameof(UserRole.Assessor));

        var previous = await db.VerificationRecords.AsNoTracking()
            .Where(v => v.CandidateId == candidateId && v.CompetencyId == competencyId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // The only VerificationRecord construction site in the codebase (see class remarks).
        var record = VerificationDecisionService.Create(
            isAssessor, candidateId, competencyId, Guid.Empty, model.FinalLevel, assessorId,
            model.Decision, model.Feedback, DateTime.UtcNow, previous?.Id);
        db.Add(record);

        var candidateCompetency = await db.CandidateCompetencies
            .FirstOrDefaultAsync(cc => cc.CandidateId == candidateId && cc.CompetencyId == competencyId, cancellationToken);
        if (candidateCompetency is null)
        {
            // HP-033, closed: a decision could previously be submitted for a competency the
            // candidate never claimed (no row yet) by visiting the URL directly — the
            // VerificationRecord above was still created (append-only, correct), but the decision
            // had nothing to update, so it silently never reached the candidate's own competency
            // status. Create the row now rather than losing the decision.
            candidateCompetency = new CandidateCompetency(candidateId, competencyId, declaredLevel: null, DateTime.UtcNow);
            db.Add(candidateCompetency);
        }

        var domainStatus = model.Decision switch
        {
            VerificationDecision.Verified => CandidateCompetencyStatus.Verified,
            VerificationDecision.Developing => CandidateCompetencyStatus.Developing,
            _ => CandidateCompetencyStatus.NotDemonstrated,
        };
        candidateCompetency.ApplyAssessorDecision(domainStatus, model.FinalLevel, DateTime.UtcNow);

        if (model.Decision == VerificationDecision.Verified)
        {
            await IssuePassportCredentialAsync(candidateId, competencyId, record, cancellationToken);
        }

        var decidedCompetencyName = await db.Competencies.AsNoTracking()
            .Where(c => c.Id == competencyId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "A competency";
        var (notificationType, notificationHeadline) = model.Decision switch
        {
            VerificationDecision.Verified => (NotificationType.CompetencyVerified, $"{decidedCompetencyName} was verified by an Assessor."),
            VerificationDecision.NotDemonstrated => (NotificationType.CompetencyNotDemonstrated, $"{decidedCompetencyName} was assessed and not demonstrated this time."),
            _ => (NotificationType.CompetencyNotDemonstrated, $"{decidedCompetencyName} was reviewed by an Assessor - you're still developing this one."),
        };
        // The Assessor's own written feedback was, until now, only ever visible on the Assessor's own
        // screen (the decision history in EvidencePack) - the candidate never saw her Assessor's actual
        // words, only this generic line. Append it when she wrote one.
        var notificationMessage = string.IsNullOrWhiteSpace(model.Feedback)
            ? notificationHeadline
            : $"{notificationHeadline} Assessor feedback: \"{model.Feedback.Trim()}\"";
        db.Add(new Notification(candidateId, notificationType, notificationMessage, competencyId, DateTime.UtcNow));

        await auditLogger.LogAsync(assessorId, "VerificationDecision", "CandidateCompetency", candidateId,
            JsonSerializer.Serialize(new { competencyId, decision = model.Decision.ToString() }), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(EvidencePack), new { candidateId, competencyId });
    }

    /// <summary>Issuing a credential follows directly from a Verified decision (docs/05 P15) — the
    /// passport is created on first verification and every later Verified decision just adds to it.</summary>
    private async Task IssuePassportCredentialAsync(Guid candidateId, Guid competencyId, VerificationRecord record, CancellationToken cancellationToken)
    {
        var existingPassport = await db.SkillsPassports
            .Include(p => p.Credentials)
            .FirstOrDefaultAsync(p => p.CandidateId == candidateId, cancellationToken);
        var isNewPassport = existingPassport is null;
        var passport = PassportIssuanceService.EnsurePassport(existingPassport, candidateId, DateTime.UtcNow);

        var competency = await db.Competencies.AsNoTracking().FirstAsync(c => c.Id == competencyId, cancellationToken);
        var slug = Regex.Replace(competency.Name, "[^A-Za-z0-9]", "").ToUpperInvariant();
        var credential = PassportIssuanceService.IssueForVerification(passport, record, slug, DateTime.UtcNow);

        if (isNewPassport)
        {
            db.Add(passport);
        }

        if (credential is not null && !isNewPassport)
        {
            db.Add(credential);
        }
    }

    /// <summary>A real, clickable permalink to the exact file (and line range, where the finding's
    /// own LineReference parses as one) at the exact commit the analysis actually ran against - never
    /// a link to the branch's current head, which could have moved since. Only built for github.com
    /// repositories (the only provider this app analyses); returns null rather than guessing for
    /// anything else, and a private repository still link out honestly - GitHub itself decides
    /// whether this assessor's own account can open it, the same as any other GitHub link.</summary>
    private static string? BuildGitHubFileLink(string repositoryUrl, string commitSha, string filePath, string lineReference)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl) || string.IsNullOrWhiteSpace(commitSha) || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalizedBase = repositoryUrl.TrimEnd('/');
        if (normalizedBase.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalizedBase = normalizedBase[..^4];
        }

        var anchor = "";
        var lineNumbers = Regex.Matches(lineReference, @"\d+").Select(m => m.Value).ToList();
        if (lineNumbers.Count == 1)
        {
            anchor = $"#L{lineNumbers[0]}";
        }
        else if (lineNumbers.Count >= 2)
        {
            anchor = $"#L{lineNumbers[0]}-L{lineNumbers[^1]}";
        }

        return $"{normalizedBase}/blob/{commitSha}/{filePath}{anchor}";
    }

    /// <summary>A one-line, honest readout of the sandbox's own automated result (never a human
    /// verdict) so the "Practical assessment reviews" list reads as distinct submissions instead of
    /// identical unlabelled buttons. Falls back to "not executed yet" rather than guessing.</summary>
    private static string SummarizeTestResults(string? testResultsJson)
    {
        if (string.IsNullOrWhiteSpace(testResultsJson))
        {
            return "Not executed yet";
        }

        try
        {
            using var doc = JsonDocument.Parse(testResultsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("sandbox", out var sandbox))
            {
                return "Not executed yet";
            }

            var executed = sandbox.TryGetProperty("Executed", out var e) && e.GetBoolean();
            if (!executed)
            {
                return "Sandbox could not run this submission";
            }

            var restoreOk = sandbox.TryGetProperty("RestoreOk", out var r) && r.GetBoolean();
            var buildOk = sandbox.TryGetProperty("BuildOk", out var b) && b.GetBoolean();
            var testsOk = sandbox.TryGetProperty("TestsOk", out var t) && t.GetBoolean();

            if (!restoreOk)
            {
                return "Failed to restore packages";
            }
            if (!buildOk)
            {
                return "Build failed";
            }
            return testsOk ? "Build succeeded, hidden tests passed" : "Build succeeded, hidden tests failed";
        }
        catch (JsonException)
        {
            return "Result unavailable";
        }
    }
}
