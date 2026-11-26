using System.Text;
using System.Text.Encodings.Web;
using HerProof.Application.Audit;
using HerProof.Application.Notifications;
using HerProof.Domain.Identity;
using HerProof.Infrastructure.Identity;
using HerProof.Infrastructure.Notifications;
using HerProof.Infrastructure.Persistence;
using HerProof.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace HerProof.Web.Controllers;

/// <summary>Hand-written rather than scaffolded Identity.UI pages, so cookie/role/ownership wiring
/// stays easy to see and test (docs/05 P03). Self-registration only ever grants Candidate: other
/// roles are provisioned out of band (seed data for the hackathon, an admin flow later).</summary>
[Route("account")]
[EnableRateLimiting("auth")]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    HerProofDbContext db,
    IEmailSender emailSender,
    IAuditLogger auditLogger,
    IWebHostEnvironment environment) : Controller
{
    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await userManager.AddToRoleAsync(user, nameof(UserRole.Candidate));
        db.CandidateProfiles.Add(new HerProof.Domain.Identity.CandidateProfile(user.Id));
        await db.SaveChangesAsync();

        await auditLogger.LogAsync(user.Id, "Register", "ApplicationUser", user.Id, "{}");

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var confirmLink = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code }, Request.Scheme)!;
        await emailSender.SendAsync(model.Email, "Confirm your HerProof account",
            EmailTemplate.WithButton(
                "Confirm your account",
                $"<p>Hi {HtmlEncoder.Default.Encode(model.FullName)},</p><p>One click and you're in — confirm your email to activate your HerProof account.</p>",
                "Confirm email",
                HtmlEncoder.Default.Encode(confirmLink)));

        // Dev/demo only: local SMTP (Mailpit) and free-tier email providers aren't reliable enough to
        // gate a hackathon rehearsal on, so the link is also shown on screen — never in Production.
        if (environment.IsDevelopment())
        {
            ViewBag.DevConfirmLink = confirmLink;
        }

        return View("RegisterConfirmation");
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string code)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await userManager.ConfirmEmailAsync(user, decodedCode);
        return View(result.Succeeded ? "ConfirmEmailSuccess" : "ConfirmEmailFailure");
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Audited as a system-level event: there is no real user id to attribute this to, and the
            // response must not reveal whether the email exists (standard login hygiene).
            await auditLogger.LogAsync(null, "LoginFailed", "ApplicationUser", Guid.Empty, "{}");
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await auditLogger.LogAsync(user.Id, "LoginSucceeded", "ApplicationUser", user.Id, "{}");
            return RedirectToLocalOrHome(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await auditLogger.LogAsync(user.Id, "LoginLockedOut", "ApplicationUser", user.Id, "{}");
            return RedirectToAction(nameof(Lockout));
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Confirm your email before signing in.");
            return View(model);
        }

        await auditLogger.LogAsync(user.Id, "LoginFailed", "ApplicationUser", user.Id, "{}");
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = userManager.GetUserId(User);
        await signInManager.SignOutAsync();
        if (userId is not null)
        {
            await auditLogger.LogAsync(Guid.Parse(userId), "Logout", "ApplicationUser", Guid.Parse(userId), "{}");
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null && await userManager.IsEmailConfirmedAsync(user))
        {
            var code = await userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var resetLink = Url.Action("ResetPassword", "Account", new { email = model.Email, code }, Request.Scheme)!;
            await emailSender.SendAsync(model.Email, "Reset your HerProof password",
                EmailTemplate.WithButton(
                    "Reset your password",
                    "<p>We received a request to reset your HerProof password. If this wasn't you, you can ignore this email.</p>",
                    "Reset password",
                    HtmlEncoder.Default.Encode(resetLink)));

            if (environment.IsDevelopment())
            {
                ViewBag.DevResetLink = resetLink;
            }
        }

        // Always the same response, confirmed account or not: don't reveal which emails are registered.
        return View("ForgotPasswordConfirmation");
    }

    [HttpGet("reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email = null, string? code = null)
    {
        if (email is null || code is null)
        {
            return BadRequest();
        }

        return View(new ResetPasswordViewModel { Email = email, Code = code });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Same confirmation view whether or not the account exists.
            return View("ResetPasswordConfirmation");
        }

        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
        var result = await userManager.ResetPasswordAsync(user, decodedCode, model.Password);
        if (result.Succeeded)
        {
            await auditLogger.LogAsync(user.Id, "PasswordReset", "ApplicationUser", user.Id, "{}");
            return View("ResetPasswordConfirmation");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet("lockout")]
    [AllowAnonymous]
    public IActionResult Lockout() => View();

    private IActionResult RedirectToLocalOrHome(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
