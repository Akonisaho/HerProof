using System.Net.Http.Json;
using HerProof.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HerProof.Infrastructure.Notifications;

/// <summary>Real transactional email via Brevo's REST API (https://api.brevo.com/v3/smtp/email),
/// used whenever Brevo:ApiKey is configured — same conditional-real-vs-fake pattern as
/// IAIService/ISpeechToText elsewhere in this codebase. Falls back to SmtpEmailSender (Mailpit) when
/// no key is set, so local dev never needs a real account.</summary>
public class BrevoEmailSender(HttpClient httpClient, IConfiguration configuration, ILogger<BrevoEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var senderEmail = configuration["Brevo:SenderEmail"] ?? "no-reply@herproof.test";
        var senderName = configuration["Brevo:SenderName"] ?? "HerProof";

        var payload = new
        {
            sender = new { name = senderName, email = senderEmail },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent = htmlBody,
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync("smtp/email", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Brevo rejected an email to {Recipient}: {Status} {Body}", toEmail, response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            // Never let a flaky provider break register/forgot-password in a demo (same fail-open
            // behaviour SmtpEmailSender has always had).
            logger.LogWarning(ex, "Failed to send email via Brevo to recipient; continuing.");
        }
    }
}
