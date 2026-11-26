using System.Security.Claims;
using HerProof.Application.Assessments;
using HerProof.Domain.Assessments;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>Assessment authoring (docs/05 P13): instructions, duration, competency mapping, rubric
/// criteria. Ownership is by creator, not organisation — an Assessor/Recruiter/Partner can only
/// manage assessments they themselves created.</summary>
[Authorize(Roles = "Recruiter,Assessor,Partner")]
[Route("assessments")]
public class AssessmentsController(HerProofDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var assessments = await db.Assessments.AsNoTracking()
            .Where(a => a.CreatedBy == userId)
            .ToListAsync(cancellationToken);
        return View(assessments);
    }

    [HttpGet("create")]
    public IActionResult Create() => View(new CreateAssessmentViewModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAssessmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var assessment = new Assessment(model.Title, model.Description, model.AssessmentType, model.DurationMinutes, GetUserId());
        if (string.Equals(model.AssessmentType, "C#", StringComparison.OrdinalIgnoreCase))
        {
            CSharpAssessmentQuestionTemplate.ApplyTo(assessment);
        }

        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { assessmentId = assessment.Id });
    }

    [HttpGet("{assessmentId:guid}")]
    public async Task<IActionResult> Details(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments
            .Include(a => a.Competencies)
            .Include(a => a.RubricCriteria)
            .Include(a => a.Questions)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.CreatedBy == GetUserId(), cancellationToken);
        if (assessment is null)
        {
            return NotFound();
        }

        ViewBag.AvailableCompetencies = await db.Competencies.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);
        ViewBag.CompetencyNames = await db.Competencies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return View(assessment);
    }

    [HttpPost("{assessmentId:guid}/competencies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCompetency(Guid assessmentId, AddAssessmentCompetencyViewModel model, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.Include(a => a.Competencies)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.CreatedBy == GetUserId(), cancellationToken);
        if (assessment is null)
        {
            return NotFound();
        }

        var mapping = assessment.CoverCompetency(model.CompetencyId, model.Weight);
        db.Add(mapping);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { assessmentId });
    }

    [HttpPost("{assessmentId:guid}/rubric-criteria")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRubricCriterion(Guid assessmentId, AddRubricCriterionViewModel model, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.Include(a => a.RubricCriteria)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.CreatedBy == GetUserId(), cancellationToken);
        if (assessment is null)
        {
            return NotFound();
        }

        var criterion = assessment.AddRubricCriterion(model.Name, model.Weight, model.Descriptor);
        db.Add(criterion);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { assessmentId });
    }

    [HttpPost("{assessmentId:guid}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.Id == assessmentId && a.CreatedBy == GetUserId(), cancellationToken);
        if (assessment is null)
        {
            return NotFound();
        }

        assessment.Publish();
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { assessmentId });
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
