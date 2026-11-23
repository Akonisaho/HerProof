using System.Security.Cryptography;
using System.Text;

namespace HerProof.Application.Passports;

/// <summary>docs/02: passport_shares.token_hash stores a hash of the share token; the raw token
/// appears only in the URL handed to the candidate, never persisted anywhere.</summary>
public static class ShareTokenGenerator
{
    public static string GenerateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
