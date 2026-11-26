using HerProof.Domain.Interviews;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace HerProof.Web.Controllers;

/// <summary>A candidate's own interview, exported as a plain PDF so she has something to attach to an
/// application or keep for her own records — same hand-rolled PdfSharp approach as
/// PassportPdfExporter, no new dependency.</summary>
internal static class InterviewReportPdfExporter
{
    public static byte[] Export(string candidateName, Interview interview, IReadOnlyDictionary<Guid, string> competencyNames)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 12, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var metaFont = new XFont("Arial", 9, XFontStyleEx.Italic);

        double y = 40;
        const double marginLeft = 40;
        const double pageBottom = 780;
        const double pageWidth = 515;

        void NewPageIfNeeded(double neededSpace)
        {
            if (y + neededSpace <= pageBottom)
            {
                return;
            }

            page = document.AddPage();
            gfx = XGraphics.FromPdfPage(page);
            y = 40;
        }

        gfx.DrawString("HerProof Interview Report", titleFont, XBrushes.Black, new XPoint(marginLeft, y));
        y += 28;
        gfx.DrawString(candidateName, headingFont, XBrushes.Black, new XPoint(marginLeft, y));
        y += 18;
        gfx.DrawString($"Interview with {interview.InterviewerPersona} · {interview.Mode} · started {interview.StartedAtUtc:yyyy-MM-dd}", metaFont, XBrushes.Gray, new XPoint(marginLeft, y));
        y += 14;
        if (interview.IsDemoData)
        {
            gfx.DrawString("Demo data — for illustration only.", metaFont, XBrushes.Gray, new XPoint(marginLeft, y));
            y += 14;
        }

        y += 16;

        foreach (var question in interview.Questions.OrderBy(q => q.QuestionOrder))
        {
            NewPageIfNeeded(80);
            var competencyName = competencyNames.GetValueOrDefault(question.CompetencyId, "(unknown competency)");
            gfx.DrawString($"Q{question.QuestionOrder + 1} · {competencyName}", headingFont, XBrushes.Black, new XPoint(marginLeft, y));
            y += 16;

            foreach (var line in WrapText(question.QuestionText, bodyFont, gfx, pageWidth))
            {
                NewPageIfNeeded(14);
                gfx.DrawString(line, bodyFont, XBrushes.Black, new XPoint(marginLeft, y));
                y += 14;
            }

            var answeredTurn = question.Turns.OrderBy(t => t.TurnIndex).LastOrDefault(t => !string.IsNullOrWhiteSpace(t.TranscriptConfirmed));
            if (answeredTurn is not null)
            {
                y += 6;
                NewPageIfNeeded(14);
                gfx.DrawString("Answer:", metaFont, XBrushes.Gray, new XPoint(marginLeft, y));
                y += 14;
                foreach (var line in WrapText(answeredTurn.TranscriptConfirmed!, bodyFont, gfx, pageWidth))
                {
                    NewPageIfNeeded(14);
                    gfx.DrawString(line, bodyFont, XBrushes.Black, new XPoint(marginLeft + 10, y));
                    y += 14;
                }

                if (answeredTurn.Evaluation is not null)
                {
                    y += 4;
                    NewPageIfNeeded(14);
                    gfx.DrawString($"Thandi's assessment: {answeredTurn.Evaluation.Verdict}", metaFont, XBrushes.Gray, new XPoint(marginLeft, y));
                    y += 14;
                    foreach (var line in WrapText(answeredTurn.Evaluation.EvidenceSummary, bodyFont, gfx, pageWidth))
                    {
                        NewPageIfNeeded(14);
                        gfx.DrawString(line, bodyFont, XBrushes.Black, new XPoint(marginLeft + 10, y));
                        y += 14;
                    }
                }
            }

            y += 18;
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
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
