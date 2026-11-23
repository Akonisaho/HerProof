using System.Security.Claims;
using HerProof.Application.Experience;
using HerProof.Domain.Competencies;
using HerProof.Domain.Experience;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Experience;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Beyond Employment (docs/05 P06): the candidate adds an experience record, a curated
/// keyword mapper proposes competencies as Suggested, and the candidate confirms or rejects each.
/// Confirming only ever claims a competency — never an award, never Verified (CLAUDE.md working
/// agreement) — the same assessment standard applies regardless of where it was acquired.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("experience")]
public class ExperienceController(HerProofDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var records = await db.ExperienceRecords
            .AsNoTracking()
            .Include(r => r.SuggestedCompetencies)
            .Where(r => r.CandidateId == candidateId)
            .ToListAsync(cancellationToken);

        var competencyIds = records.SelectMany(r => r.SuggestedCompetencies).Select(sc => sc.CompetencyId).Distinct().ToList();
        ViewBag.CompetencyNames = await db.Competencies.AsNoTracking()
            .Where(c => competencyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return View(records);
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new CreateExperienceRecordViewModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateExperienceRecordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var record = new ExperienceRecord(candidateId.Value, model.Category, model.Title, model.Description, model.StartDate, model.EndDate);

        var nonTraditionalCompetencies = await db.Competencies.AsNoTracking()
            .Where(c => c.Category == CompetencyCategory.NonTraditional && c.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var competencyId in ExperienceCompetencyMapper.Suggest(record, nonTraditionalCompetencies))
        {
            record.SuggestCompetency(competencyId);
        }

        db.ExperienceRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mappings/{experienceCompetencyId:guid}/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid experienceCompetencyId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var mapping = await db.ExperienceCompetencies
            .Join(db.ExperienceRecords, m => m.ExperienceRecordId, r => r.Id, (m, r) => new { Mapping = m, Record = r })
            .Where(x => x.Mapping.Id == experienceCompetencyId && x.Record.CandidateId == candidateId)
            .Select(x => x.Mapping)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping is null)
        {
            return NotFound();
        }

        mapping.Confirm();

        var existing = await db.CandidateCompetencies
            .FirstOrDefaultAsync(cc => cc.CandidateId == candidateId && cc.CompetencyId == mapping.CompetencyId, cancellationToken);
        var claimed = BeyondEmploymentConfirmation.EnsureClaimed(candidateId.Value, mapping.CompetencyId, declaredLevel: null, existing, DateTime.UtcNow);
        if (existing is null)
        {
            db.CandidateCompetencies.Add(claimed);
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mappings/{experienceCompetencyId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid experienceCompetencyId, CancellationToken cancellationToken)
    {
        var candidateId = await GetCandidateProfileIdAsync(cancellationToken);
        if (candidateId is null)
        {
            return NotFound();
        }

        var mapping = await db.ExperienceCompetencies
            .Join(db.ExperienceRecords, m => m.ExperienceRecordId, r => r.Id, (m, r) => new { Mapping = m, Record = r })
            .Where(x => x.Mapping.Id == experienceCompetencyId && x.Record.CandidateId == candidateId)
            .Select(x => x.Mapping)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapping is null)
        {
            return NotFound();
        }

        mapping.Reject();
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<Guid?> GetCandidateProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }
}
