using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using HerProof.Application.Audit;
using HerProof.Application.Passports;
using HerProof.Application.Verification;
using HerProof.Domain.Assessments;
using HerProof.Domain.Competencies;
using HerProof.Domain.Identity;
using HerProof.Domain.Interviews;
using HerProof.Domain.Verification;
using HerProof.Infrastructure.Persistence;
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

        ViewBag.UnreadKeys = unreadKeys;
        ViewBag.UnreadCount = unreadKeys.Count;

        var ordered = pending
            .OrderByDescending(cc => unreadKeys.Contains($"{cc.CandidateId}:{cc.CompetencyId}"))
            .ThenByDescending(cc => cc.UpdatedAtUtc)
            .ToList();

        return View(ordered);
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
        var executionReports = await db.ExecutionReports.AsNoTracking()
            .Where(r => candidateAnalysisIds.Contains(r.AnalysisId))
            .ToListAsync(cancellationToken);

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

        var assessmentIdsForCompetency = await db.AssessmentCompetencies.AsNoTracking()
            .Where(ac => ac.CompetencyId == competencyId)
            .Select(ac => ac.AssessmentId)
            .ToListAsync(cancellationToken);
        var candidateSessionIds = await db.AssessmentSessions.AsNoTracking()
            .Where(s => s.CandidateId == candidateId && assessmentIdsForCompetency.Contains(s.AssessmentId))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var submissionIds = await db.AssessmentSubmissions.AsNoTracking()
            .Where(s => candidateSessionIds.Contains(s.SessionId))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var assessmentReviews = await db.AssessmentReviews.AsNoTracking()
            .Where(r => submissionIds.Contains(r.SubmissionId))
            .ToListAsync(cancellationToken);
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
            ExecutionReports = executionReports,
            InterviewTurns = interviewTurns,
            AssessmentReviews = assessmentReviews,
            IntegrityTimeline = integrityTimeline,
            DecisionHistory = decisionHistory,
            AiSuggestion = aiSuggestion,
        };

        ViewBag.CandidateId = candidateId;
        ViewBag.CompetencyId = competencyId;
        ViewBag.IsDemoData = candidateAnalyses.Any(a => a.IsDemoData) || candidateInterviews.Any(i => i.IsDemoData);
        ViewBag.RepositoryContributorStatuses = candidateRepositories
            .Where(r => candidateAnalyses.Any(a => a.RepositoryId == r.Id))
            .Select(r => r.ContributorStatus)
            .ToList();

        var assessorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await auditLogger.LogAsync(assessorId, "EvidencePackViewed", "CandidateCompetency", candidateId,
            JsonSerializer.Serialize(new { competencyId }), cancellationToken);

        return View(viewModel);
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
}
