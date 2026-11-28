using HerProof.Domain.Interviews;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace HerProof.Web.Controllers;

/// <summary>A candidate's own interview, exported as a branded PDF so she has something professional
/// to attach to an application or keep for her own records — same hand-rolled PdfSharp approach as
/// PassportPdfExporter, no new dependency.</summary>
internal static class InterviewReportPdfExporter
{
    private static readonly XColor BrandPink = XColor.FromArgb(0xc2, 0x18, 0x6b);
    private static readonly XColor BrandPinkDarker = XColor.FromArgb(0x19, 0x0c, 0x13);
    private static readonly XColor InkMuted = XColor.FromArgb(0x6b, 0x66, 0x70);
    private static readonly XColor VerdictGreen = XColor.FromArgb(0x2f, 0x7a, 0x4d);
    private static readonly XColor VerdictAmber = XColor.FromArgb(0xb0, 0x83, 0x00);
    private static readonly XColor VerdictGrey = XColor.FromArgb(0x6c, 0x75, 0x7d);

    private const double MarginLeft = 44;
    private const double PageBottom = 780;
    private const double PageWidth = 507;

    public static byte[] Export(string candidateName, Interview interview, IReadOnlyDictionary<Guid, string> competencyNames, string? verifyUrl = null)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 12, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var labelFont = new XFont("Arial", 8, XFontStyleEx.Bold);
        var metaFont = new XFont("Arial", 9, XFontStyleEx.Regular);

        double y = 0;

        void NewPageIfNeeded(double neededSpace)
        {
            if (y + neededSpace <= PageBottom)
            {
                return;
            }

            page = document.AddPage();
            gfx = XGraphics.FromPdfPage(page);
            y = 40;
        }

        void DrawWrapped(string text, XFont font, XBrush brush, double x, double maxWidth)
        {
            foreach (var line in WrapText(text, font, gfx, maxWidth))
            {
                NewPageIfNeeded(14);
                gfx.DrawString(line, font, brush, new XPoint(x, y));
                y += 14;
            }
        }

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
        double cy = y + 22;

        XFont CenterFont(double size, XFontStyleEx style) => new("Arial", size, style);
        void DrawCentered(string text, XFont font, XBrush brush)
        {
            var width = gfx.MeasureString(text, font).Width;
            gfx.DrawString(text, font, brush, new XPoint(centerX - width / 2, cy));
        }

        DrawCentered("INTERVIEW RECORD", CenterFont(9, XFontStyleEx.Bold), new XSolidBrush(BrandPink));
        cy += 22;
        DrawCentered(candidateName, CenterFont(16, XFontStyleEx.Bold), XBrushes.Black);
        cy += 20;
        DrawCentered($"Completed an AI-assisted skills interview with {interview.InterviewerPersona}", CenterFont(9.5, XFontStyleEx.Regular), new XSolidBrush(InkMuted));
        cy += 14;
        DrawCentered($"{interview.Mode} mode  ·  {interview.StartedAtUtc:d MMMM yyyy}", CenterFont(9.5, XFontStyleEx.Regular), new XSolidBrush(InkMuted));
        cy += 18;
        if (interview.IsDemoData)
        {
            DrawCentered("Demo data — shown as a reliable fallback, not a live answer session.", CenterFont(8.5, XFontStyleEx.Italic), new XSolidBrush(InkMuted));
            cy += 14;
        }

        DrawCentered(
            "This is Nolwazi's recommendation only — not a verified credential. A named human",
            CenterFont(8, XFontStyleEx.Italic), new XSolidBrush(InkMuted));
        cy += 11;
        DrawCentered(
            "Assessor reviews this evidence and makes the final verification decision.",
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

        y += boxHeight + 20;

        // --- Summary strip ---
        var evaluated = interview.Questions
            .SelectMany(q => q.Turns)
            .Where(t => t.Evaluation is not null)
            .Select(t => t.Evaluation!.Verdict)
            .ToList();
        var understood = evaluated.Count(v => v == Verdict.Understood);
        var partial = evaluated.Count(v => v == Verdict.Partial);
        var notDemonstrated = evaluated.Count(v => v == Verdict.NotDemonstrated);

        y += 16;
        DrawSummaryTile(gfx, MarginLeft, y, "Understood", understood, VerdictGreen, labelFont);
        DrawSummaryTile(gfx, MarginLeft + 175, y, "Partial", partial, VerdictAmber, labelFont);
        DrawSummaryTile(gfx, MarginLeft + 350, y, "Not demonstrated", notDemonstrated, VerdictGrey, labelFont);
        y += 60;

        gfx.DrawLine(new XPen(XColor.FromArgb(0xf3, 0xd2, 0xe4), 1), MarginLeft, y, MarginLeft + PageWidth, y);
        y += 20;

        // --- Questions ---
        foreach (var question in interview.Questions.OrderBy(q => q.QuestionOrder))
        {
            NewPageIfNeeded(90);
            var competencyName = competencyNames.GetValueOrDefault(question.CompetencyId, "Unknown competency");
            gfx.DrawString($"Q{question.QuestionOrder + 1}  ·  {competencyName}", headingFont, new XSolidBrush(BrandPink), new XPoint(MarginLeft, y));
            y += 16;

            DrawWrapped(question.QuestionText, bodyFont, XBrushes.Black, MarginLeft, PageWidth);

            if (!string.IsNullOrEmpty(question.SnippetFilePath))
            {
                NewPageIfNeeded(14);
                gfx.DrawString($"{question.SnippetFilePath}  lines {question.SnippetLines}", metaFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));
                y += 16;
            }

            var answeredTurn = question.Turns.OrderBy(t => t.TurnIndex).LastOrDefault(t => !string.IsNullOrWhiteSpace(t.TranscriptConfirmed));
            if (answeredTurn is not null)
            {
                NewPageIfNeeded(14);
                gfx.DrawString("CANDIDATE'S ANSWER", labelFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));
                y += 14;
                DrawWrapped(answeredTurn.TranscriptConfirmed!, bodyFont, XBrushes.Black, MarginLeft + 8, PageWidth - 8);

                if (answeredTurn.Evaluation is not null)
                {
                    y += 6;
                    NewPageIfNeeded(14);
                    var (verdictText, verdictColor) = answeredTurn.Evaluation.Verdict switch
                    {
                        Verdict.Understood => ("UNDERSTOOD", VerdictGreen),
                        Verdict.Partial => ("PARTIALLY UNDERSTOOD", VerdictAmber),
                        Verdict.NotDemonstrated => ("NOT DEMONSTRATED", VerdictGrey),
                        _ => (answeredTurn.Evaluation.Verdict.ToString().ToUpperInvariant(), VerdictGrey),
                    };
                    gfx.DrawString($"NOLWAZI'S ASSESSMENT — {verdictText}", labelFont, new XSolidBrush(verdictColor), new XPoint(MarginLeft, y));
                    y += 14;
                    DrawWrapped(answeredTurn.Evaluation.EvidenceSummary, bodyFont, XBrushes.Black, MarginLeft + 8, PageWidth - 8);
                }
            }
            else
            {
                NewPageIfNeeded(14);
                gfx.DrawString("Not yet answered.", bodyFont, new XSolidBrush(InkMuted), new XPoint(MarginLeft, y));
                y += 14;
            }

            y += 20;
        }

        NewPageIfNeeded(40);
        y += 10;
        gfx.DrawLine(new XPen(XColor.FromArgb(0xf3, 0xd2, 0xe4), 1), MarginLeft, y, MarginLeft + PageWidth, y);
        y += 16;
        DrawWrapped(
            "This is Nolwazi's recommendation only, grounded in the candidate's own code and her confirmed transcript. A named human Assessor reviews this evidence and makes the final verification decision — never AI.",
            new XFont("Arial", 8.5, XFontStyleEx.Italic), new XSolidBrush(InkMuted), MarginLeft, PageWidth);

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void DrawSummaryTile(XGraphics gfx, double x, double y, string label, int count, XColor color, XFont labelFont)
    {
        gfx.DrawString(count.ToString(), new XFont("Arial", 22, XFontStyleEx.Bold), new XSolidBrush(color), new XPoint(x, y + 22));
        gfx.DrawString(label.ToUpperInvariant(), labelFont, new XSolidBrush(InkMuted), new XPoint(x, y + 36));
    }

    private static IEnumerable<string> WrapText(string text, XFont font, XGraphics gfx, double maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
        {
            yield return line;
        }
    }
}
