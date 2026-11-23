using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HerProof.Application.AI;

namespace HerProof.AI;

/// <summary>Used whenever no real provider key is configured (local dev, CI, demo without network).
/// NOT a placeholder pretending to be real analysis — it is the real, documented fallback path
/// (docs/01 section 15 / docs/06 section 10: cached/demo result when the provider is unavailable),
/// deterministic from the same untrusted content every real analyzer gets, so prompt-injection and
/// verification logic are exercised identically whether or not a real LLM key exists. Handles both
/// shapes this codebase asks an LLM for: repository analysis (P08) and Thandi interview turns (P10).</summary>
public partial class FakeAiService : IAIService
{
    public Task<AiCompletion> CompleteJsonAsync(string systemPrompt, string untrustedUserContent, string promptVersion, CancellationToken cancellationToken = default)
    {
        var json = systemPrompt.Contains("repository analysis agent", StringComparison.Ordinal)
            ? BuildRepositoryAnalysisResponse(untrustedUserContent)
            : untrustedUserContent.Contains("Generate 4 to 6 interview questions", StringComparison.Ordinal)
                ? BuildQuestionGenerationResponse(untrustedUserContent)
                : BuildTurnEvaluationResponse();

        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt + untrustedUserContent)));
        return Task.FromResult(new AiCompletion(json, "fake-heuristic-v1", promptVersion, inputHash));
    }

    private static string BuildRepositoryAnalysisResponse(string untrustedUserContent)
    {
        var fileBlocks = ParseFileBlocks(untrustedUserContent);
        var findings = new List<object>();
        var technologies = new HashSet<string>();

        foreach (var (path, content) in fileBlocks)
        {
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && content.Length > 20)
            {
                findings.Add(new
                {
                    competencyName = "Problem-solving",
                    findingType = "readme-context",
                    filePath = path,
                    lineReference = (string?)null,
                    description = "README describes the problem the project solves.",
                    evidence = "Non-trivial README present.",
                    confidence = "Low",
                });
                technologies.Add("Documentation");
            }
        }

        var payload = new { summary = $"Fake/demo analysis of {fileBlocks.Count} file(s). No real LLM configured.", technologiesDetected = technologies.ToList(), findings };
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>Builds real, verifiable questions by citing the same evidence lines
    /// InterviewPromptBuilder embedded in the prompt ("- [Competency] type at path:lines — desc"), so
    /// SnippetReferenceVerifier passes them exactly as a real model's well-formed answer would.</summary>
    private static string BuildQuestionGenerationResponse(string untrustedUserContent)
    {
        var evidenceLines = EvidenceLinePattern().Matches(untrustedUserContent);
        var questions = new List<object>();
        var order = 1;

        foreach (Match match in evidenceLines)
        {
            if (questions.Count >= 4)
            {
                break;
            }

            var competencyName = match.Groups["competency"].Value;
            var path = match.Groups["path"].Value;
            var lines = match.Groups["lines"].Value;
            questions.Add(new
            {
                competencyName,
                text = $"I looked at {path} around line {lines}. Can you walk me through what's happening there, question {order}?",
                snippetFilePath = path,
                snippetLines = lines,
                expectedPoints = new[] { "explains intent", "explains mechanics" },
                difficulty = 2,
            });
            order++;
        }

        return JsonSerializer.Serialize(new { questions });
    }

    private static string BuildTurnEvaluationResponse() => JsonSerializer.Serialize(new
    {
        verdictSoFar = "Partial",
        coveredPoints = Array.Empty<string>(),
        missingPoints = Array.Empty<string>(),
        contradictions = Array.Empty<object>(),
        nextAction = "move_on",
        probeKind = (string?)null,
        nextUtterance = "Thanks for walking me through that — let's move on to the next question.",
        snippetFilePath = (string?)null,
        snippetLines = (string?)null,
    });

    private static List<(string Path, string Content)> ParseFileBlocks(string untrustedUserContent) => untrustedUserContent
        .Split("--- FILE: ", StringSplitOptions.RemoveEmptyEntries)
        .Skip(1)
        .Select(block => block.Split(" ---", 2))
        .Where(parts => parts.Length == 2)
        .Select(parts => (Path: parts[0].Trim(), Content: parts[1]))
        .ToList();

    [GeneratedRegex(@"- \[(?<competency>[^\]]+)\] \S+ at (?<path>\S+):(?<lines>\S+) —")]
    private static partial Regex EvidenceLinePattern();
}
