using HerProof.Infrastructure.Sandbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HerProof.Integration.Tests;

/// <summary>Fast, no-Docker-required tests for the feature flag. The real container-isolation
/// behaviour (malicious fixtures: fork bomb, huge output, network call) is covered by
/// SandboxMaliciousFixtureTests, gated behind HERPROOF_SANDBOX_TESTS=1 since it needs a real Docker
/// daemon and the built herproof-sandbox image — not something CI can assume (docs/05 P09).</summary>
public class DockerSandboxRunnerTests
{
    [Fact]
    public async Task When_execution_is_disabled_it_reports_not_executed_and_never_runs_docker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Execution:Enabled"] = "false" })
            .Build();
        var runner = new DockerSandboxRunner(configuration, NullLogger<DockerSandboxRunner>.Instance);

        var result = await runner.RunAsync("/tmp/does-not-matter");

        Assert.False(result.Executed);
        Assert.False(result.RestoreOk);
        Assert.False(result.BuildOk);
        Assert.Contains("not executed", result.LogExcerpt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_execution_flag_is_absent_it_defaults_to_disabled()
    {
        // Never execute untrusted code unless explicitly opted in (CLAUDE.md rule 6).
        var configuration = new ConfigurationBuilder().Build();
        var runner = new DockerSandboxRunner(configuration, NullLogger<DockerSandboxRunner>.Instance);

        var result = await runner.RunAsync("/tmp/does-not-matter");

        Assert.False(result.Executed);
    }
}
