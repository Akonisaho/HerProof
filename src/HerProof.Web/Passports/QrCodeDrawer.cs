using PdfSharp.Drawing;

namespace HerProof.Web.Controllers;

/// <summary>Draws a QR module matrix directly as filled rectangles, including a quiet zone (required
/// for a scanner to reliably find the code), so PDF exports never depend on PdfSharp's PNG decoder.</summary>
internal static class QrCodeDrawer
{
    private const int QuietZoneModules = 2;

    public static void Draw(XGraphics gfx, bool[,] matrix, double x, double y, double size)
    {
        var moduleCount = matrix.GetLength(0);
        var totalModules = moduleCount + QuietZoneModules * 2;
        var moduleSize = size / totalModules;

        gfx.DrawRectangle(XBrushes.White, x, y, size, size);

        var brush = new XSolidBrush(XColors.Black);
        for (var row = 0; row < moduleCount; row++)
        {
            for (var col = 0; col < moduleCount; col++)
            {
                if (!matrix[row, col])
                {
                    continue;
                }

                var moduleX = x + (col + QuietZoneModules) * moduleSize;
                var moduleY = y + (row + QuietZoneModules) * moduleSize;
                gfx.DrawRectangle(brush, moduleX, moduleY, moduleSize, moduleSize);
            }
        }
    }
}
