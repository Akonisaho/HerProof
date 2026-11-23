namespace HerProof.Application.AI;

public record AiCompletion(string RawJson, string ModelName, string PromptVersion, string InputHash);

/// <summary>The only way any code in this codebase talks to an LLM (ADR-03). Implementations must
/// request JSON-only output and never expose any tool that writes verification state (docs/03
/// section 6 rule 4) — this interface physically cannot call anything but return text, which is the
/// enforcement mechanism: there is no tool-use surface here to misuse.</summary>
public interface IAIService
{
    Task<AiCompletion> CompleteJsonAsync(string systemPrompt, string untrustedUserContent, string promptVersion, CancellationToken cancellationToken = default);
}
