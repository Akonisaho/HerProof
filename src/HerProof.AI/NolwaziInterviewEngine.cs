using System.Text.Json;
using HerProof.Application.AI;
using HerProof.Application.Interviews;
using HerProof.Application.Repositories;
using Microsoft.Extensions.Logging;

namespace HerProof.AI;

/// <summary>Nolwazi, text-first (docs/06). Generates questions grounded in real repo evidence and
/// evaluates each answer turn, with the same discipline as the repository analysis pipeline: schema
/// validation, snippet verification against the pinned commit, a forbidden-content filter on every
/// piece of free text, a hard probe cap, and retry-once-then-fail-safe on bad model output.</summary>
public class NolwaziInterviewEngine(IAIService aiService, string interviewerName, ILogger<NolwaziInterviewEngine>? logger = null)
{
    public const string PromptVersion = "thandi-interview-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(
        IReadOnlyList<StaticFinding> staticFindings,
        IReadOnlyList<LlmFinding> analysisFindings,
        IReadOnlyList<string> requiredCompetencyNames,
        FetchedRepositoryContent content,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = InterviewPromptBuilder.BuildSystemPrompt(interviewerName);
        var userContent = InterviewPromptBuilder.BuildQuestionGenerationUserContent(staticFindings, analysisFindings, requiredCompetencyNames);

        var dto = await TryCompleteAsync<QuestionGenerationDto>(systemPrompt, userContent, cancellationToken)
            ?? await TryCompleteAsync<QuestionGenerationDto>(systemPrompt, userContent, cancellationToken);

        if (dto?.Questions is null)
        {
            logger?.LogWarning("Nolwazi question generation: model response could not be parsed into any questions at all (both attempts failed).");
            return [];
        }

        var kept = new List<GeneratedQuestion>();
        foreach (var q in dto.Questions)
        {
            if (string.IsNullOrWhiteSpace(q.CompetencyName) || string.IsNullOrWhiteSpace(q.Text))
            {
                logger?.LogWarning("Nolwazi question dropped: missing competency name or text.");
                continue;
            }

            if (!SnippetReferenceVerifier.Exists(content, q.SnippetFilePath, q.SnippetLines))
            {
                var available = string.Join(", ", content.Files.Select(f => f.Path));
                logger?.LogWarning(
                    "Nolwazi question dropped: snippet {Path}:{Lines} not found in fetched content. Fetched files were: {Available}",
                    q.SnippetFilePath, q.SnippetLines, available);
                continue;
            }

            if (ForbiddenContentFilter.ContainsForbiddenContent(q.Text))
            {
                logger?.LogWarning("Nolwazi question dropped: forbidden content filter matched.");
                continue;
            }

            kept.Add(new GeneratedQuestion(q.CompetencyName, q.Text, q.SnippetFilePath, q.SnippetLines, q.ExpectedPoints ?? [], q.Difficulty));
        }

        logger?.LogInformation("Nolwazi question generation: {Kept}/{Total} questions survived filtering.", kept.Count, dto.Questions.Count);
        return kept;
    }

    public async Task<TurnEvaluationResult?> EvaluateTurnAsync(
        string questionText,
        string snippetFilePath,
        string snippetLines,
        string snippetContent,
        string confirmedTranscript,
        int probeCountSoFar,
        FetchedRepositoryContent content,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = InterviewPromptBuilder.BuildSystemPrompt(interviewerName);
        var userContent = InterviewPromptBuilder.BuildTurnEvaluationUserContent(
            questionText, snippetFilePath, snippetLines, snippetContent, confirmedTranscript, probeCountSoFar);

        var dto = await TryCompleteAsync<TurnEvaluationDto>(systemPrompt, userContent, cancellationToken)
            ?? await TryCompleteAsync<TurnEvaluationDto>(systemPrompt, userContent, cancellationToken);

        if (dto is null || !TryParseNextAction(dto.NextAction, out var nextAction))
        {
            return null;
        }

        // Hard cap regardless of what the model says: no follow-ups at all - every question moves on
        // after exactly one answer. Fast by design for a live demo with a deliberately short question
        // set (docs/00-PITCH.md demo script).
        if (nextAction is NextAction.Probe or NextAction.Clarify)
        {
            nextAction = NextAction.MoveOn;
        }

        var utterance = dto.NextUtterance ?? string.Empty;
        if (ForbiddenContentFilter.ContainsForbiddenContent(utterance))
        {
            // Never let a forbidden-content utterance reach the candidate; fail safe to a neutral move-on.
            return new TurnEvaluationResult(
                dto.VerdictSoFar ?? "Partial", dto.CoveredPoints ?? [], dto.MissingPoints ?? [],
                MapContradictions(dto.Contradictions), NextAction.MoveOn, null,
                "Thank you — let's move on to the next question.", null, null);
        }

        if (!string.IsNullOrWhiteSpace(dto.SnippetFilePath)
            && !SnippetReferenceVerifier.Exists(content, dto.SnippetFilePath, dto.SnippetLines))
        {
            // A probe/clarify that cites a snippet must cite a real one; if it doesn't, drop the
            // citation rather than the whole evaluation (docs/01 section 6's discard rule, applied
            // at field granularity here since the verdict itself may still be sound).
            return new TurnEvaluationResult(
                dto.VerdictSoFar ?? "Partial", dto.CoveredPoints ?? [], dto.MissingPoints ?? [],
                MapContradictions(dto.Contradictions), nextAction, dto.ProbeKind, utterance, null, null);
        }

        return new TurnEvaluationResult(
            dto.VerdictSoFar ?? "Partial", dto.CoveredPoints ?? [], dto.MissingPoints ?? [],
            MapContradictions(dto.Contradictions), nextAction, dto.ProbeKind, utterance, dto.SnippetFilePath, dto.SnippetLines);
    }

    /// <summary>docs/06 section 7 uses snake_case action names ("move_on", "finish_question").</summary>
    private static bool TryParseNextAction(string? value, out NextAction action)
    {
        action = value?.Trim().ToLowerInvariant() switch
        {
            "probe" => NextAction.Probe,
            "clarify" => NextAction.Clarify,
            "move_on" => NextAction.MoveOn,
            "finish_question" => NextAction.FinishQuestion,
            _ => (NextAction)(-1),
        };
        return (int)action >= 0;
    }

    private static List<Contradiction> MapContradictions(List<ContradictionDto>? dtos) =>
        (dtos ?? []).Select(c => new Contradiction(c.CandidateSaid ?? "", c.CodeShows ?? "", c.File ?? "", c.Lines ?? "")).ToList();

    private async Task<T?> TryCompleteAsync<T>(string systemPrompt, string userContent, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var completion = await aiService.CompleteJsonAsync(systemPrompt, userContent, PromptVersion, cancellationToken);
            var normalizedJson = LlmJsonKeyNormalizer.ToCamelCaseKeys(completion.RawJson);
            logger?.LogInformation("Nolwazi raw AI JSON: {Raw}", completion.RawJson);
            return JsonSerializer.Deserialize<T>(normalizedJson, JsonOptions);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested == false)
        {
            logger?.LogWarning(ex, "Nolwazi AI completion failed to parse.");
            return null;
        }
    }

    private record QuestionGenerationDto(List<QuestionDto>? Questions);

    private record QuestionDto(string CompetencyName, string Text, string SnippetFilePath, string SnippetLines, List<string>? ExpectedPoints, int Difficulty);

    private record TurnEvaluationDto(
        string? VerdictSoFar,
        List<string>? CoveredPoints,
        List<string>? MissingPoints,
        List<ContradictionDto>? Contradictions,
        string NextAction,
        string? ProbeKind,
        string? NextUtterance,
        string? SnippetFilePath,
        string? SnippetLines);

    private record ContradictionDto(string? CandidateSaid, string? CodeShows, string? File, string? Lines);
}
