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
        return pngQrCode.GetGraphic(10);
    }
}
