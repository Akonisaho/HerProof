namespace HerProof.Application.Interviews;

public record GeneratedQuestion(string CompetencyName, string Text, string SnippetFilePath, string SnippetLines, IReadOnlyList<string> ExpectedPoints, int Difficulty);

public record Contradiction(string CandidateSaid, string CodeShows, string File, string Lines);

public enum NextAction
{
    Probe,
    Clarify,
    MoveOn,
    FinishQuestion,
}

public record TurnEvaluationResult(
    string VerdictSoFar,
    IReadOnlyList<string> CoveredPoints,
    IReadOnlyList<string> MissingPoints,
    IReadOnlyList<Contradiction> Contradictions,
    NextAction NextAction,
    string? ProbeKind,
    string NextUtterance,
    string? SnippetFilePath,
    string? SnippetLines);
