using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HerProof.Web.Controllers;

/// <summary>Deterministic signed token for an interview's public certificate QR: HMAC-SHA256(secret,
/// interviewId), truncated to 16 hex characters. Nothing is stored and there is no expiry - a finished
/// certificate should stay scannable for as long as the candidate keeps the PDF, same as a printed one.</summary>
internal static class InterviewCertificateToken
{
    public static string Generate(Guid interviewId, IConfiguration configuration) => Sign(interviewId, configuration);

    public static bool IsValid(Guid interviewId, string? token, IConfiguration configuration) =>
        !string.IsNullOrEmpty(token)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(interviewId, configuration)),
            Encoding.UTF8.GetBytes(token));

    private static string Sign(Guid interviewId, IConfiguration configuration)
    {
        var secret = configuration["InterviewCertificate:Secret"]
            ?? configuration["SessionEventToken:Secret"]
            ?? "dev_only_change_me_session_event_token_secret";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(interviewId.ToString()));
        return Convert.ToHexString(hash)[..16];
    }
}
