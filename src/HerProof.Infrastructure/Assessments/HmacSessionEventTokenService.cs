using System.Security.Cryptography;
using System.Text;
using HerProof.Application.Assessments;
using Microsoft.Extensions.Configuration;

namespace HerProof.Infrastructure.Assessments;

/// <summary>Self-contained signed token: sessionId + expiry + HMAC-SHA256(secret, sessionId.expiry).
/// No database lookup needed to validate, and scoped to nothing but this one endpoint — it cannot be
/// used to do anything else, unlike a general bearer token would be. STUB(HP-025) until real OAuth.</summary>
public class HmacSessionEventTokenService(IConfiguration configuration) : ISessionEventTokenService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8); // Covers the longest assessment plus buffer.

    public string Issue(Guid sessionId, DateTime nowUtc)
    {
        var expiry = nowUtc.Add(Lifetime).Ticks;
        var payload = $"{sessionId}.{expiry}";
        var signature = Sign(payload);
        return $"{payload}.{signature}";
    }

    public Guid? Validate(string token, DateTime nowUtc)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var payload = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(parts[2])))
        {
            return null;
        }

        if (!Guid.TryParse(parts[0], out var sessionId) || !long.TryParse(parts[1], out var expiryTicks))
        {
            return null;
        }

        return nowUtc.Ticks > expiryTicks ? null : sessionId;
    }

    private string Sign(string payload)
    {
        var secret = configuration["SessionEventToken:Secret"] ?? "dev_only_change_me_session_event_token_secret";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash);
    }
}
