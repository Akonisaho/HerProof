using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HerProof.Web.Controllers;

/// <summary>Deterministic signed token for a passport's general certificate QR - same pattern as
/// InterviewCertificateToken: HMAC-SHA256(secret, passportId), no expiry, nothing stored. The
/// certificate page itself always re-reads current credentials live, so there is nothing stale to
/// invalidate even though the token never changes.</summary>
internal static class PassportCertificateToken
{
    public static string Generate(Guid passportId, IConfiguration configuration) => Sign(passportId, configuration);

    public static bool IsValid(Guid passportId, string? token, IConfiguration configuration) =>
        !string.IsNullOrEmpty(token)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(passportId, configuration)),
            Encoding.UTF8.GetBytes(token));

    private static string Sign(Guid passportId, IConfiguration configuration)
    {
        var secret = configuration["PassportCertificate:Secret"]
            ?? configuration["SessionEventToken:Secret"]
            ?? "dev_only_change_me_session_event_token_secret";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"passport.{passportId}"));
        return Convert.ToHexString(hash)[..16];
    }
}
