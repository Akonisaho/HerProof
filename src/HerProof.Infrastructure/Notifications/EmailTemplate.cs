namespace HerProof.Infrastructure.Notifications;

/// <summary>Wraps plain email content in HerProof's brand shell (same palette as wwwroot/css/brand.css)
/// so transactional email doesn't look like an unstyled default. Table-based layout and inline styles
/// throughout: this has to render in Mailpit, Gmail and Outlook alike, none of which reliably support
/// a &lt;style&gt; block in email.</summary>
public static class EmailTemplate
{
    private const string Pink = "#c2186b";
    private const string PinkDark = "#96104f";
    private const string PinkDarker = "#190c13";
    private const string PinkSoft = "#fdf0f6";
    private const string Ink = "#15121a";
    private const string InkMuted = "#6b6670";

    /// <summary>A short message plus one call-to-action button (confirm email, reset password, ...).</summary>
    public static string WithButton(string heading, string bodyHtml, string buttonText, string buttonUrl) =>
        Shell(
            heading,
            $"""
            {bodyHtml}
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:28px 0 4px;">
              <tr>
                <td style="border-radius:999px;background:{Pink};">
                  <a href="{buttonUrl}" style="display:inline-block;padding:13px 28px;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:999px;">{buttonText}</a>
                </td>
              </tr>
            </table>
            <p style="margin:18px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:12.5px;line-height:1.6;color:{InkMuted};">
              Button not working? Paste this link into your browser:<br />
              <a href="{buttonUrl}" style="color:{PinkDark};word-break:break-all;">{buttonUrl}</a>
            </p>
            """);

    /// <summary>A plain message with no call-to-action, e.g. a notification or a thank-you note.</summary>
    public static string Plain(string heading, string bodyHtml) => Shell(heading, bodyHtml);

    private static string Shell(string heading, string innerHtml) =>
        $"""
        <!doctype html>
        <html lang="en">
        <body style="margin:0;padding:0;background:{PinkSoft};">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{PinkSoft};padding:32px 16px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" style="max-width:480px;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #f3d2e4;">
                  <tr>
                    <td style="background:{PinkDarker};padding:22px 28px;">
                      <span style="font-family:Georgia,'Times New Roman',serif;font-weight:800;font-size:19px;color:#ffffff;letter-spacing:-0.02em;">
                        Her<span style="font-style:italic;color:#f3a9c6;">Proof</span>
                      </span>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:32px 28px 28px;">
                      <h1 style="margin:0 0 14px;font-family:'Segoe UI',Arial,sans-serif;font-size:21px;font-weight:800;color:{Ink};letter-spacing:-0.01em;">{heading}</h1>
                      <div style="font-family:'Segoe UI',Arial,sans-serif;font-size:14.5px;line-height:1.65;color:{Ink};">
                        {innerHtml}
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td style="background:{PinkDarker};padding:18px 28px;">
                      <p style="margin:0;font-family:'Segoe UI',Arial,sans-serif;font-size:11.5px;color:#9b8f95;">
                        Evidence before verification, always. &middot; Built for the SheCode Hackathon.
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
