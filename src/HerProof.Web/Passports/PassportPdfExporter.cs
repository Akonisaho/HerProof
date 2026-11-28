using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace HerProof.Web.Controllers;

/// <summary>Candidate's own full passport, exported as a branded certificate PDF (docs/05 P15).
/// Unlike the public verify page, this authenticated self-view is not limited to a share's
/// allow-list, because only the owning candidate can request it (PassportController.ExportPdf).</summary>
internal static class PassportPdfExporter
{
    private static readonly XColor BrandPink = XColor.FromArgb(0xc2, 0x18, 0x6b);
    private static readonly XColor BrandPinkDarker = XColor.FromArgb(0x19, 0x0c, 0x13);
    private static readonly XColor InkMuted = XColor.FromArgb(0x6b, 0x66, 0x70);
    private static readonly XColor VerdictGreen = XColor.FromArgb(0x2f, 0x7a, 0x4d);

    private const double MarginLeft = 44;
    private const double PageWidth = 507;

    public static byte[] Export(
        string candidateName,
        string passportNumber,
        IReadOnlyList<(string CompetencyName, string CredentialIdentifier, DateTime IssuedAtUtc, DateTime? ValidUntilUtc, string Status)> credentials,
        string? verifyUrl = null)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var headingFont = new XFont("Arial", 12, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var labelFont = new XFont("Arial", 8, XFontStyleEx.Bold);
        var metaFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);

        double y;

        // --- Letterhead ---
        gfx.DrawRectangle(new XSolidBrush(BrandPinkDarker), 0, 0, page.Width.Point, 70);
        gfx.DrawString("HerProof", titleFont, new XSolidBrush(XColors.White), new XPoint(MarginLeft, 30));
        gfx.DrawString("Evidence before verification", metaFont, new XSolidBrush(XColor.FromArgb(0xf3, 0xa9, 0xc6)), new XPoint(MarginLeft, 50));

        // --- Certificate box: double border, centered name, QR to the public verify page ---
        y = 96;
        const double boxHeight = 150;
        var boxPen = new XPen(BrandPink, 1.5);
        gfx.DrawRectangle(boxPen, MarginLeft, y, PageWidth, boxHeight);
        gfx.DrawRectangle(boxPen, MarginLeft + 4, y + 4, PageWidth - 8, boxHeight - 8);

        var textWidth = verifyUrl is null ? PageWidth : PageWidth - 110;
        var centerX = MarginLeft + textWidth / 2;
        double cy = y + 26;

        XFont CenterFont(double size, XFontStyleEx style) => new("Arial", size, style);
        void DrawCentered(string text, XFont font, XBrush brush)
        {
            var width = gfx.MeasureString(text, font).Width;
            gfx.DrawString(text, font, brush, new XPoint(centerX - width / 2, cy));
        }

        DrawCentered("SKILLS PASSPORT", CenterFont(9, XFontStyleEx.Bold), new XSolidBrush(BrandPink));
        cy += 22;
        DrawCentered(candidateName, CenterFont(16, XFontStyleEx.Bold), XBrushes.Black);
        cy += 20;
        DrawCentered($"Passport number {passportNumber}", CenterFont(9.5, XFontStyleEx.Regular), new XSolidBrush(InkMuted));
        cy += 14;
        DrawCentered(
            $"{credentials.Count(c => c.Status == "Active")} verified competenc{(credentials.Count(c => c.Status == "Active") == 1 ? "y" : "ies")}",
            CenterFont(9.5, XFontStyleEx.Regular), new XSolidBrush(InkMuted));
        cy += 20;
        DrawCentered(
            "A named human Assessor reviewed the evidence for each competency below.",
            CenterFont(8, XFontStyleEx.Italic), new XSolidBrush(InkMuted));

        if (verifyUrl is not null)
        {
            const double qrSize = 80;
            var qrX = MarginLeft + PageWidth - qrSize - 14;
            var qrY = y + (boxHeight - qrSize) / 2 - 8;
            QrCodeDrawer.Draw(gfx, QrCodeGenerator.GenerateMatrix(verifyUrl), qrX, qrY, qrSize);
            var captionFont = new XFont("Arial", 7.5, XFontStyleEx.Regular);
            var captionWidth = gfx.MeasureString("Scan to verify", captionFont).Width;
            gfx.DrawString("Scan to verify", captionFont, new XSolidBrush(InkMuted), new XPoint(qrX + qrSize / 2 - captionWidth / 2, qrY + qrSize + 10));
        }

        y += boxHeight + 24;
        gfx.DrawLine(new XPen(XColor.FromArgb(0xf3, 0xd2, 0xe4), 1), MarginLeft, y, MarginLeft + PageWidth, y);
        y += 20;

        gfx.DrawString("Verified competencies", headingFont, new XSolidBrush(BrandPinkDarker), new XPoint(MarginLeft, y));
        y += 24;

        if (credentials.Count == 0)
        {
            gfx.DrawString("No verified credentials yet.", bodyFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));
        }

        foreach (var credential in credentials)
        {
            var validity = credential.ValidUntilUtc is null
                ? "no expiry"
                : $"valid until {credential.ValidUntilUtc:yyyy-MM-dd}";

            gfx.DrawString(credential.CompetencyName, headingFont, new XSolidBrush(BrandPink), new XPoint(MarginLeft, y));
            var statusWidth = gfx.MeasureString(credential.Status, labelFont).Width;
            gfx.DrawString(credential.Status, labelFont, new XSolidBrush(VerdictGreen), new XPoint(MarginLeft + PageWidth - statusWidth, y - 2));
            y += 16;
            gfx.DrawString(credential.CredentialIdentifier, bodyFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));
            y += 14;
            gfx.DrawString($"Issued {credential.IssuedAtUtc:yyyy-MM-dd} - {validity}", metaFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));
            y += 14;
            gfx.DrawLine(new XPen(XColor.FromArgb(0xf3, 0xd2, 0xe4), 1), MarginLeft, y, MarginLeft + PageWidth, y);
            y += 18;
        }

        y += 10;
        var footerFont = new XFont("Arial", 8.5, XFontStyleEx.Italic);
        gfx.DrawString(
            "Issued by HerProof. No recordings, transcripts, repositories, contact details or scores are ever disclosed through this document.",
            footerFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
