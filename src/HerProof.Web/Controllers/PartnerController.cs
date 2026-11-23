using HerProof.Application.Notifications;
using HerProof.Domain.Partnerships;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Partnerships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HerProof.Web.Controllers;

/// <summary>docs/STUBS.md HP-033, closed: real "Partner with us" intake replacing a mailto: link that
/// left no record of who asked. Public, anonymous — this is a sales lead form, not an authenticated
/// action, so it only ever creates a PartnershipInquiry; it can't touch roles or organisations itself
/// (an Administrator does that from /admin/organisations after making contact).</summary>
[Route("partner")]
[AllowAnonymous]
[EnableRateLimiting("partner")]
public class PartnerController(HerProofDbContext db, IEmailSender emailSender, IConfiguration configuration) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(new PartnerInquiryViewModel());

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(PartnerInquiryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var inquiry = new PartnershipInquiry(
            model.OrganisationName,
            model.ContactName,
            model.ContactEmail,
            model.Phone,
            model.InquiryType,
            model.Message,
            DateTime.UtcNow);

        db.PartnershipInquiries.Add(inquiry);
        await db.SaveChangesAsync(cancellationToken);

        var notifyEmail = configuration["Partner:NotificationEmail"];
        if (!string.IsNullOrWhiteSpace(notifyEmail))
        {
            await emailSender.SendAsync(
                notifyEmail,
                $"New partner inquiry: {model.OrganisationName}",
                $"""
                <p><strong>{System.Net.WebUtility.HtmlEncode(model.OrganisationName)}</strong> ({model.InquiryType})</p>
                <p>Contact: {System.Net.WebUtility.HtmlEncode(model.ContactName)} &lt;{System.Net.WebUtility.HtmlEncode(model.ContactEmail)}&gt;{(string.IsNullOrWhiteSpace(model.Phone) ? "" : $" / {System.Net.WebUtility.HtmlEncode(model.Phone)}")}</p>
                <p>{System.Net.WebUtility.HtmlEncode(model.Message)}</p>
                """,
                cancellationToken);
        }

        await emailSender.SendAsync(
            model.ContactEmail,
            "Thanks for reaching out to HerProof",
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(model.ContactName)},</p>" +
            "<p>Thanks for your interest in partnering with HerProof. A member of our team will be in touch shortly.</p>",
            cancellationToken);

        return View("ThankYou");
    }
}
