namespace HerProof.Application.Assessments;

public record CriterionScore(Guid CriterionId, string Name, decimal Weight, decimal Score, string Rationale);

public record ScoringResult(IReadOnlyList<CriterionScore> CriterionScores, decimal OverallScore);

/// <summary>Deliberately simple, deterministic auto-evaluation (docs/05 P13): sandbox build/test
/// results are the only signal, applied uniformly to every rubric criterion. This produces a
/// recommendation only — it never creates a VerificationRecord (CLAUDE.md rule 1); a human assessor
/// reviews it alongside the evidence pack (P14).</summary>
public static class AssessmentScoring
{
    public static ScoringResult Score(
        IReadOnlyList<(Guid CriterionId, string Name, decimal Weight)> criteria,
        bool restoreOk,
        bool buildOk,
        bool testsOk,
        bool executed)
    {
        // Not executed (sandbox disabled or unavailable): never fabricate a score (docs/01 section 15).
        var baseScore = !executed ? 0m : !restoreOk ? 0m : !buildOk ? 0.25m : testsOk ? 1m : 0.5m;
        var rationale = !executed
            ? "Not executed — sandbox unavailable or disabled; no automated evidence."
            : !restoreOk
                ? "Dependency restore failed."
                : !buildOk
                    ? "Restored but did not build."
                    : testsOk
                        ? "Built and all tests passed."
                        : "Built but one or more tests failed.";

        var scores = criteria
            .Select(c => new CriterionScore(c.CriterionId, c.Name, c.Weight, baseScore, rationale))
            .ToList();

        var totalWeight = criteria.Sum(c => c.Weight);
        var overall = totalWeight == 0 ? 0m : scores.Sum(s => s.Score * s.Weight) / totalWeight;

        return new ScoringResult(scores, overall);
    }
}
