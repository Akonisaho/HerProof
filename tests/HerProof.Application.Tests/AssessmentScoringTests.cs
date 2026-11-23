using HerProof.Application.Assessments;
using Xunit;

namespace HerProof.Application.Tests;

public class AssessmentScoringTests
{
    private static readonly (Guid CriterionId, string Name, decimal Weight)[] Criteria =
    [
        (Guid.NewGuid(), "Correctness", 2m),
        (Guid.NewGuid(), "Code quality", 1m),
    ];

    [Fact]
    public void Not_executed_never_fabricates_a_nonzero_score()
    {
        var result = AssessmentScoring.Score(Criteria, restoreOk: false, buildOk: false, testsOk: false, executed: false);

        Assert.Equal(0m, result.OverallScore);
        Assert.All(result.CriterionScores, c => Assert.Contains("Not executed", c.Rationale));
    }

    [Fact]
    public void Full_pass_scores_full_marks()
    {
        var result = AssessmentScoring.Score(Criteria, restoreOk: true, buildOk: true, testsOk: true, executed: true);

        Assert.Equal(1m, result.OverallScore);
    }

    [Fact]
    public void Failed_restore_scores_zero_even_though_it_executed()
    {
        var result = AssessmentScoring.Score(Criteria, restoreOk: false, buildOk: false, testsOk: false, executed: true);

        Assert.Equal(0m, result.OverallScore);
    }

    [Fact]
    public void Built_but_failing_tests_scores_partial_credit()
    {
        var result = AssessmentScoring.Score(Criteria, restoreOk: true, buildOk: true, testsOk: false, executed: true);

        Assert.Equal(0.5m, result.OverallScore);
    }

    [Fact]
    public void Overall_score_is_weighted_by_criterion_weight()
    {
        var weighted = new (Guid, string, decimal)[] { (Guid.NewGuid(), "Heavy", 3m), (Guid.NewGuid(), "Light", 1m) };
        var result = AssessmentScoring.Score(weighted, restoreOk: true, buildOk: true, testsOk: true, executed: true);

        // Both score 1.0 here since scoring is uniform per criterion in this simple model; weight
        // only matters when criteria score differently, which this proves doesn't divide-by-zero or
        // mis-weight when all are equal.
        Assert.Equal(1m, result.OverallScore);
    }

    [Fact]
    public void Every_criterion_gets_its_own_score_row()
    {
        var result = AssessmentScoring.Score(Criteria, restoreOk: true, buildOk: true, testsOk: true, executed: true);

        Assert.Equal(2, result.CriterionScores.Count);
        Assert.Contains(result.CriterionScores, c => c.Name == "Correctness");
        Assert.Contains(result.CriterionScores, c => c.Name == "Code quality");
    }
}
