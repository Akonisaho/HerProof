using System.Net.Mail;
using HerProof.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HerProof.Infrastructure.Notifications;

/// <summary>Local dev / demo only: sends through Mailpit (docker-compose), no auth, no TLS.
/// STUB(HP-004): swap for a real provider (and secrets, not plain SMTP) before any real deployment.</summary>
public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = configuration["Smtp:Host"] ?? "localhost";
        var port = configuration.GetValue<int?>("Smtp:Port") ?? 1025;

        using var client = new SmtpClient(host, port);
        using var message = new MailMessage("no-reply@herproof.test", toEmail, subject, htmlBody) { IsBodyHtml = true };

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex)
        {
            // Never let a flaky local mail sink break register/forgot-password in a demo.
            logger.LogWarning(ex, "Failed to send email to recipient; continuing.");
        }
    }
}
