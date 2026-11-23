using System.Text.Json;
using HerProof.Application.AI;
using HerProof.Application.Repositories;

namespace HerProof.AI;

public record RepositoryAnalysisResult(
    IReadOnlyList<StaticFinding> StaticFindings,
    IReadOnlyList<LlmFinding> VerifiedLlmFindings,
    int DiscardedLlmFindingCount,
    int SecretsRedacted,
    string Summary,
    IReadOnlyList<string> TechnologiesDetected,
    string ModelName,
    string PromptVersion,
    string InputHash);

/// <summary>Ties together the whole P08 pipeline: redact -> Roslyn (deterministic) -> LLM
/// (judgement) -> verify every LLM reference against the pinned commit -> combine. ADR-09: Roslyn
/// anchors the LLM; the LLM's claims never reach a finding unless FindingVerification confirms them.</summary>
public class RepositoryAnalysisOrchestrator(IAIService aiService, IStaticCodeAnalyzer staticAnalyzer)
{
    public const string PromptVersion = "repo-analysis-v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RepositoryAnalysisResult> AnalyzeAsync(FetchedRepositoryContent content, CancellationToken cancellationToken = default)
    {
        var secretsRedacted = 0;
        var redactedFiles = content.Files
            .Select(f =>
            {
                var redaction = SecretRedactor.Redact(f.Content);
                secretsRedacted += redaction.SecretsFound;
                return new FetchedRepositoryFile(f.Path, redaction.RedactedContent);
            })
            .ToList();
        var redactedContent = new FetchedRepositoryContent(content.CommitSha, redactedFiles);

        var staticFindings = staticAnalyzer.Analyze(redactedFiles);

        var systemPrompt = RepositoryAnalysisPromptBuilder.BuildSystemPrompt();
        var userContent = RepositoryAnalysisPromptBuilder.BuildUserContent(redactedFiles);

        // docs/03 section 6 rule 2: validate JSON, retry once, then fail safe (never crash the pipeline
        // over a single bad LLM response — static findings are still real evidence on their own).
        var parsed = await TryCompleteAndParseAsync(systemPrompt, userContent, cancellationToken)
            ?? await TryCompleteAndParseAsync(systemPrompt, userContent, cancellationToken);

        if (parsed is null)
        {
            return new RepositoryAnalysisResult(staticFindings, [], 0, secretsRedacted,
                "The AI analysis could not be parsed after retrying; only static analysis evidence is available.",
                [], "none", PromptVersion, "none");
        }

        var (completion, summary, technologies, llmFindings) = parsed.Value;
        var verified = FindingVerification.VerifyAndFilter(llmFindings, redactedContent);

        return new RepositoryAnalysisResult(
            staticFindings, verified, llmFindings.Count - verified.Count, secretsRedacted,
            summary, technologies, completion.ModelName, completion.PromptVersion, completion.InputHash);
    }

    private async Task<(AiCompletion Completion, string Summary, List<string> Technologies, List<LlmFinding> Findings)?> TryCompleteAndParseAsync(
        string systemPrompt, string userContent, CancellationToken cancellationToken)
    {
        AiCompletion completion;
        try
        {
            completion = await aiService.CompleteJsonAsync(systemPrompt, userContent, PromptVersion, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested == false)
        {
            return null; // Provider error: treated the same as invalid output — fail safe, let the caller retry/stop.
        }

        try
        {
            var dto = JsonSerializer.Deserialize<LlmAnalysisDto>(completion.RawJson, JsonOptions);
            if (dto?.Findings is null)
            {
                return null;
            }

            var findings = dto.Findings
                .Where(f => !string.IsNullOrWhiteSpace(f.CompetencyName) && !string.IsNullOrWhiteSpace(f.FilePath))
                .Select(f => new LlmFinding(f.CompetencyName, f.FindingType, f.FilePath, f.LineReference, f.Description, f.Evidence, f.Confidence))
                .ToList();

            return (completion, dto.Summary ?? string.Empty, dto.TechnologiesDetected ?? [], findings);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record LlmAnalysisDto(string? Summary, List<string>? TechnologiesDetected, List<LlmFindingDto>? Findings);

    private record LlmFindingDto(string CompetencyName, string FindingType, string FilePath, string? LineReference, string Description, string Evidence, string Confidence);
}
