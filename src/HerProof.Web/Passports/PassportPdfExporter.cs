using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace HerProof.Web.Controllers;

/// <summary>Candidate's own full passport, exported as a plain PDF (docs/05 P15). This is the
/// authenticated self-view: unlike the public verify page it is not limited to a share's allow-list,
/// because only the owning candidate can request it (PassportController.ExportPdf).</summary>
internal static class PassportPdfExporter
{
    public static byte[] Export(string candidateName, string passportNumber, IReadOnlyList<(string CompetencyName, string CredentialIdentifier, DateTime IssuedAtUtc, DateTime? ValidUntilUtc, string Status)> credentials)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 12, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 11, XFontStyleEx.Regular);

        double y = 40;
        gfx.DrawString("HerProof Skills Passport", titleFont, XBrushes.Black, new XPoint(40, y));
        y += 30;
        gfx.DrawString(candidateName, headingFont, XBrushes.Black, new XPoint(40, y));
        y += 20;
        gfx.DrawString($"Passport number: {passportNumber}", bodyFont, XBrushes.Black, new XPoint(40, y));
        y += 30;

        gfx.DrawString("Verified competencies", headingFont, XBrushes.Black, new XPoint(40, y));
        y += 24;

        foreach (var credential in credentials)
        {
            var validity = credential.ValidUntilUtc is null
                ? "no expiry"
                : $"valid until {credential.ValidUntilUtc:yyyy-MM-dd}";
            gfx.DrawString($"- {credential.CompetencyName} ({credential.Status})", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 16;
            gfx.DrawString($"  {credential.CredentialIdentifier} - issued {credential.IssuedAtUtc:yyyy-MM-dd}, {validity}", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
        }

        if (credentials.Count == 0)
        {
            gfx.DrawString("No verified credentials yet.", bodyFont, XBrushes.Black, new XPoint(50, y));
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
