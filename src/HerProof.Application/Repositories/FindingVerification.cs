namespace HerProof.Application.Repositories;

/// <summary>A finding from the LLM whose file/line reference doesn't actually exist in the fetched,
/// pinned-commit content (docs/01 section 6: "A finding whose file or line reference cannot be
/// verified against the pinned commit is discarded"). This is the one and only gate between an LLM's
/// claim and a stored RepositoryFinding — every finding must pass through it.</summary>
public record LlmFinding(string CompetencyName, string FindingType, string FilePath, string? LineReference, string Description, string Evidence, string Confidence);

public static class FindingVerification
{
    public static IReadOnlyList<LlmFinding> VerifyAndFilter(IReadOnlyList<LlmFinding> findings, FetchedRepositoryContent content) =>
        findings.Where(f => !string.IsNullOrWhiteSpace(f.FilePath) && SnippetReferenceVerifier.Exists(content, f.FilePath, f.LineReference)).ToList();
}
