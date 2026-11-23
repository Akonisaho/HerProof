using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Audit;
using HerProof.Domain.Identity;
using HerProof.Domain.Partnerships;
using HerProof.Infrastructure.Identity;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Authorization;
using HerProof.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HerProof.Web.Controllers;

/// <summary>docs/STUBS.md HP-005, closed: real UI for an Administrator to grant Recruiter/Assessor/
/// Partner/Administrator roles and set up organisations — previously only the seeded demo accounts
/// had these roles at all. Candidate is never granted here; self-registration already does that, and
/// nothing about this controller can ever touch a VerificationRecord or a competency decision.</summary>
[Authorize(Policy = Policies.RequireAdministrator)]
[Route("admin")]
public class AdminController(HerProofDbContext db, UserManager<ApplicationUser> userManager, IAuditLogger auditLogger) : Controller
{
    [HttpGet("users")]
    public async Task<IActionResult> Users(string? search, CancellationToken cancellationToken)
    {
        var usersQuery = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            usersQuery = usersQuery.Where(u => u.Email!.Contains(normalized) || u.FullName.Contains(normalized));
        }

        var users = await usersQuery.OrderBy(u => u.Email).Take(100).ToListAsync(cancellationToken);
        var userIds = users.Select(u => u.Id).ToList();

        var roleAssignments = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, role.Name }).ToListAsync(cancellationToken);
        var rolesByUser = roleAssignments
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name!).ToList());

        var rows = users.Select(u => new AdminUserRow
        {
            UserId = u.Id,
            Email = u.Email ?? "(no email)",
            FullName = u.FullName,
            Roles = rolesByUser.GetValueOrDefault(u.Id, []),
        }).ToList();

        ViewBag.Search = search;
        return View(rows);
    }

    [HttpGet("users/{userId:guid}/roles")]
    public async Task<IActionResult> AssignRoles(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        ViewBag.UserEmail = user.Email;
        ViewBag.UserId = userId;
        return View(new AssignRolesViewModel
        {
            Recruiter = currentRoles.Contains(nameof(UserRole.Recruiter)),
            Assessor = currentRoles.Contains(nameof(UserRole.Assessor)),
            Partner = currentRoles.Contains(nameof(UserRole.Partner)),
            Administrator = currentRoles.Contains(nameof(UserRole.Administrator)),
        });
    }

    [HttpPost("users/{userId:guid}/roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRoles(Guid userId, AssignRolesViewModel model, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var desired = new Dictionary<string, bool>
        {
            [nameof(UserRole.Recruiter)] = model.Recruiter,
            [nameof(UserRole.Assessor)] = model.Assessor,
            [nameof(UserRole.Partner)] = model.Partner,
            [nameof(UserRole.Administrator)] = model.Administrator,
        };

        var currentRoles = await userManager.GetRolesAsync(user);
        foreach (var (roleName, shouldHave) in desired)
        {
            var currentlyHas = currentRoles.Contains(roleName);
            if (shouldHave && !currentlyHas)
            {
                await userManager.AddToRoleAsync(user, roleName);
            }
            else if (!shouldHave && currentlyHas)
            {
                await userManager.RemoveFromRoleAsync(user, roleName);
            }
        }

        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await auditLogger.LogAsync(adminId, "RoleAssignment", "ApplicationUser", userId,
            JsonSerializer.Serialize(desired), cancellationToken);

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("organisations")]
    public async Task<IActionResult> Organisations(CancellationToken cancellationToken)
    {
        var organisations = await db.Organisations.AsNoTracking().ToListAsync(cancellationToken);
        var memberCounts = await db.OrganisationMembers.AsNoTracking()
            .GroupBy(m => m.OrganisationId)
            .Select(g => new { OrganisationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrganisationId, x => x.Count, cancellationToken);

        var rows = organisations.Select(o => new OrganisationRow
        {
            Id = o.Id,
            Name = o.Name,
            MemberCount = memberCounts.GetValueOrDefault(o.Id, 0),
        }).ToList();

        return View(rows);
    }

    [HttpPost("organisations/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrganisation(CreateOrganisationViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["AdminError"] = "Organisation name is required.";
            return RedirectToAction(nameof(Organisations));
        }

        var organisation = new Domain.Identity.Organisation(model.Name, model.Industry, model.Website);
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(OrganisationDetail), new { organisationId = organisation.Id });
    }

    [HttpGet("organisations/{organisationId:guid}")]
    public async Task<IActionResult> OrganisationDetail(Guid organisationId, CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return NotFound();
        }

        var members = await db.OrganisationMembers.AsNoTracking()
            .Where(m => m.OrganisationId == organisationId)
            .ToListAsync(cancellationToken);
        var userIds = members.Select(m => m.UserId).ToList();
        var emailsByUserId = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? "(no email)", cancellationToken);

        var rows = members
            .Select(m => (m.Id, emailsByUserId.GetValueOrDefault(m.UserId, "(unknown)"), m.OrganisationRole))
            .ToList();

        return View(new OrganisationDetailViewModel { Organisation = organisation, Members = rows });
    }

    [HttpPost("organisations/{organisationId:guid}/members")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOrganisationMember(Guid organisationId, AddOrganisationMemberViewModel model, CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return NotFound();
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            TempData["AdminError"] = $"No user found with email {model.Email}.";
            return RedirectToAction(nameof(OrganisationDetail), new { organisationId });
        }

        var alreadyMember = await db.OrganisationMembers.AsNoTracking()
            .AnyAsync(m => m.OrganisationId == organisationId && m.UserId == user.Id, cancellationToken);
        if (!alreadyMember)
        {
            db.OrganisationMembers.Add(new Domain.Identity.OrganisationMember(organisationId, user.Id, model.Role));
        }

        // Organisation membership alone doesn't satisfy [Authorize(Roles=...)] on Recruiter/Partner
        // controllers — grant the matching Identity role too, the same way AssignRoles does.
        var roleName = model.Role switch
        {
            OrganisationRole.Recruiter => nameof(UserRole.Recruiter),
            OrganisationRole.Partner => nameof(UserRole.Partner),
            _ => null,
        };
        if (roleName is not null && !await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(OrganisationDetail), new { organisationId });
    }

    [HttpPost("organisations/{organisationId:guid}/members/{memberId:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOrganisationMember(Guid organisationId, Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.OrganisationMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.OrganisationId == organisationId, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        db.OrganisationMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(OrganisationDetail), new { organisationId });
    }

    [HttpGet("partner-inquiries")]
    public async Task<IActionResult> PartnerInquiries(CancellationToken cancellationToken)
    {
        var rows = await db.PartnershipInquiries.AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new PartnerInquiryRow
            {
                Id = i.Id,
                OrganisationName = i.OrganisationName,
                ContactName = i.ContactName,
                ContactEmail = i.ContactEmail,
                Phone = i.Phone,
                InquiryType = i.InquiryType,
                Message = i.Message,
                Status = i.Status,
                CreatedAtUtc = i.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpPost("partner-inquiries/{inquiryId:guid}/contacted")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPartnerInquiryContacted(Guid inquiryId, CancellationToken cancellationToken)
    {
        var inquiry = await db.PartnershipInquiries.FirstOrDefaultAsync(i => i.Id == inquiryId, cancellationToken);
        if (inquiry is null)
        {
            return NotFound();
        }

        inquiry.MarkContacted();
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(PartnerInquiries));
    }

    [HttpPost("partner-inquiries/{inquiryId:guid}/close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClosePartnerInquiry(Guid inquiryId, CancellationToken cancellationToken)
    {
        var inquiry = await db.PartnershipInquiries.FirstOrDefaultAsync(i => i.Id == inquiryId, cancellationToken);
        if (inquiry is null)
        {
            return NotFound();
        }

        inquiry.MarkClosed();
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(PartnerInquiries));
    }
}
