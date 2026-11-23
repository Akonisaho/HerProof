namespace HerProof.Application.Sandbox;

public record SandboxExecutionResult(bool Executed, bool RestoreOk, bool BuildOk, bool TestsOk, string LogExcerpt, string? SandboxImageDigest);

/// <summary>Builds and tests untrusted candidate code (docs/03 section 5). The web host must never
/// run candidate code (CLAUDE.md rule 6) — only this runner, via a disposable, network-isolated
/// container, ever does. If isolation isn't available, `Executed` is false and the result says so
/// honestly (docs/01 section 15) — it never fabricates a pass or fail.</summary>
public interface ISandboxRunner
{
    Task<SandboxExecutionResult> RunAsync(string readOnlyInputDirectory, CancellationToken cancellationToken = default);
}
