using System.Text.Json;
using HerProof.Application.AI;
using HerProof.Application.Interviews;
using HerProof.Application.Repositories;

namespace HerProof.AI;

/// <summary>Thandi, text-first (docs/06). Generates questions grounded in real repo evidence and
/// evaluates each answer turn, with the same discipline as the repository analysis pipeline: schema
/// validation, snippet verification against the pinned commit, a forbidden-content filter on every
/// piece of free text, a hard probe cap, and retry-once-then-fail-safe on bad model output.</summary>
public class ThandiInterviewEngine(IAIService aiService, string interviewerName)
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
            return [];
        }

        return dto.Questions
            .Where(q => !string.IsNullOrWhiteSpace(q.CompetencyName) && !string.IsNullOrWhiteSpace(q.Text))
            .Where(q => SnippetReferenceVerifier.Exists(content, q.SnippetFilePath, q.SnippetLines))
            .Where(q => !ForbiddenContentFilter.ContainsForbiddenContent(q.Text))
            .Select(q => new GeneratedQuestion(q.CompetencyName, q.Text, q.SnippetFilePath, q.SnippetLines, q.ExpectedPoints ?? [], q.Difficulty))
            .ToList();
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

        // Hard cap regardless of what the model says (docs/06 section 4): three probes, no more.
        if (nextAction == NextAction.Probe && probeCountSoFar >= 3)
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
            return JsonSerializer.Deserialize<T>(completion.RawJson, JsonOptions);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested == false)
        {
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
