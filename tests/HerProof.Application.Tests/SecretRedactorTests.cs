using HerProof.Application.Repositories;
using Xunit;

namespace HerProof.Application.Tests;

public class SecretRedactorTests
{
    [Fact]
    public void Redacts_aws_access_keys()
    {
        var result = SecretRedactor.Redact("var key = \"AKIAIOSFODNN7EXAMPLE\";");

        Assert.Equal(1, result.SecretsFound);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", result.RedactedContent);
    }

    [Fact]
    public void Redacts_generic_api_key_assignments()
    {
        var result = SecretRedactor.Redact("API_KEY = \"sk-abcdefghijklmnopqrstuvwxyz123456\"");

        Assert.True(result.SecretsFound > 0);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz123456", result.RedactedContent);
    }

    [Fact]
    public void Redacts_private_key_blocks()
    {
        var content = "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA...\n-----END RSA PRIVATE KEY-----";

        var result = SecretRedactor.Redact(content);

        Assert.Equal(1, result.SecretsFound);
        Assert.DoesNotContain("MIIEpAIBAAKCAQEA", result.RedactedContent);
    }

    [Fact]
    public void Does_not_flag_placeholder_dev_passwords()
    {
        var result = SecretRedactor.Redact("Password=dev_only_change_me;Host=localhost");

        Assert.Equal(0, result.SecretsFound);
    }

    [Fact]
    public void Ordinary_code_with_no_secrets_is_untouched()
    {
        var content = "public class OrderService { public void Process(Order order) { } }";

        var result = SecretRedactor.Redact(content);

        Assert.Equal(0, result.SecretsFound);
        Assert.Equal(content, result.RedactedContent);
    }
}
