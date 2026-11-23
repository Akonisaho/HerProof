namespace HerProof.Application.Repositories;

public record StaticFinding(string CompetencyName, string FindingType, string FilePath, string? LineReference, string Description, string Evidence);

/// <summary>Deterministic, non-LLM evidence (ADR-09: "Roslyn for C# static facts plus LLM for
/// judgement — deterministic evidence anchors the LLM"). These findings need no verification step
/// (docs/01 section 6) because they're derived directly from the fetched, pinned-commit source, not
/// claimed by an untrusted model.</summary>
public interface IStaticCodeAnalyzer
{
    IReadOnlyList<StaticFinding> Analyze(IReadOnlyList<FetchedRepositoryFile> files);
}
