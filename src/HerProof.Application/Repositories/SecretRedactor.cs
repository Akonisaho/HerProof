using System.Text.RegularExpressions;

namespace HerProof.Application.Repositories;

public record RedactionResult(string RedactedContent, int SecretsFound);

/// <summary>Redacts likely secrets from file content before it ever reaches an LLM prompt (docs/03
/// section 6 rule 6, CLAUDE.md rule 7). Found secrets are reported as a security finding, not
/// silently dropped — the candidate and assessor should know a secret was committed.</summary>
public static partial class SecretRedactor
{
    private static readonly (string Label, Regex Pattern)[] Patterns =
    [
        ("AWS Access Key", AwsAccessKeyPattern()),
        ("Generic API Key", ApiKeyPattern()),
        ("Private Key Block", PrivateKeyBlockPattern()),
        ("Password Assignment", PasswordAssignmentPattern()),
        ("Connection String Password", ConnectionStringPasswordPattern()),
    ];

    public static RedactionResult Redact(string content)
    {
        var secretsFound = 0;
        var result = content;

        foreach (var (label, pattern) in Patterns)
        {
            result = pattern.Replace(result, match =>
            {
                secretsFound++;
                return $"[REDACTED:{label}]";
            });
        }

        return new RedactionResult(result, secretsFound);
    }

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}")]
    private static partial Regex AwsAccessKeyPattern();

    [GeneratedRegex(@"(?i)(api[_-]?key|secret[_-]?key|access[_-]?token)\s*[:=]\s*[""']?[A-Za-z0-9\-_]{16,}[""']?")]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----[\s\S]*?-----END (RSA |EC |OPENSSH )?PRIVATE KEY-----")]
    private static partial Regex PrivateKeyBlockPattern();

    [GeneratedRegex(@"(?i)password\s*[:=]\s*[""']?(?!your_|changeme|dev_only)[^\s""';]{6,}[""']?")]
    private static partial Regex PasswordAssignmentPattern();

    [GeneratedRegex(@"(?i)(Password|Pwd)\s*=\s*(?!your_|changeme|dev_only)[^;""']{4,}")]
    private static partial Regex ConnectionStringPasswordPattern();
}
