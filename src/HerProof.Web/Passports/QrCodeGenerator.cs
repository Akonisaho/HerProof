using QRCoder;

namespace HerProof.Web.Controllers;

/// <summary>Thin wrapper over QRCoder's pure-C# PNG renderer (no System.Drawing dependency, so it
/// works the same on the Linux CI/sandbox host as on a Windows dev box).</summary>
internal static class QrCodeGenerator
{
    public static byte[] GeneratePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        return pngQrCode.GetGraphic(10, new byte[] { 0, 0, 0, 255 }, new byte[] { 255, 255, 255, 255 });
    }

    /// <summary>The raw dark/light module grid, for callers (PDF exporters) that draw the QR code as
    /// vector rectangles instead of decoding a PNG - PdfSharp's own PNG decoder corrupts QRCoder's
    /// output (renders as diagonal noise) on some filter types, so PDFs never embed the PNG.</summary>
    public static bool[,] GenerateMatrix(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var rows = data.ModuleMatrix;
        var size = rows.Count;
        var matrix = new bool[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                matrix[y, x] = rows[y][x];
            }
        }

        return matrix;
    }
}
