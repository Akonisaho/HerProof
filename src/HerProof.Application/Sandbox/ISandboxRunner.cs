namespace HerProof.Application.Sandbox;

public record SandboxExecutionResult(bool Executed, bool RestoreOk, bool BuildOk, bool TestsOk, string LogExcerpt, string? SandboxImageDigest);

/// <summary>Which real, isolated Docker image a submission's sandbox run uses - each value maps to its
/// own image and its own entrypoint script, never a shared image pretending to support a language it
/// doesn't actually execute. Kept separate from HerProof.Domain.Assessments.AssessmentLanguage so this
/// layer doesn't need to know about the Assessments domain at all - callers map one to the other.</summary>
public enum SandboxLanguage
{
    CSharp,
    Java,
}

/// <summary>Builds and tests untrusted candidate code (docs/03 section 5). The web host must never
/// run candidate code (CLAUDE.md rule 6) — only this runner, via a disposable, network-isolated
/// container, ever does. If isolation isn't available, `Executed` is false and the result says so
/// honestly (docs/01 section 15) — it never fabricates a pass or fail.</summary>
public interface ISandboxRunner
{
    Task<SandboxExecutionResult> RunAsync(string readOnlyInputDirectory, SandboxLanguage language = SandboxLanguage.CSharp, CancellationToken cancellationToken = default);
}
