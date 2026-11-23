using HerProof.Application.Opportunities;
using HerProof.Domain.Opportunities;
using Xunit;

namespace HerProof.Application.Tests;

public class EligibilityRuleEvaluatorTests
{
    private static readonly CandidateEligibilityData BlankCandidate = new(null, null, null, null, null, null);

    [Fact]
    public void Equals_rule_is_met_when_candidate_value_matches()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.Province, EligibilityOperator.Equals, "Gauteng", true, "");
        var candidate = BlankCandidate with { Province = "Gauteng" };

        Assert.Equal(EligibilityResult.Met, EligibilityRuleEvaluator.Evaluate(rule, candidate));
    }

    [Fact]
    public void Equals_rule_is_case_insensitive()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.Province, EligibilityOperator.Equals, "gauteng", true, "");
        var candidate = BlankCandidate with { Province = "GAUTENG" };

        Assert.Equal(EligibilityResult.Met, EligibilityRuleEvaluator.Evaluate(rule, candidate));
    }

    [Fact]
    public void Equals_rule_is_not_met_when_candidate_value_differs()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.Province, EligibilityOperator.Equals, "Gauteng", true, "");
        var candidate = BlankCandidate with { Province = "Limpopo" };

        Assert.Equal(EligibilityResult.NotMet, EligibilityRuleEvaluator.Evaluate(rule, candidate));
    }

    [Theory]
    [InlineData(EligibilityRuleType.Province)]
    [InlineData(EligibilityRuleType.GenderDeclared)]
    [InlineData(EligibilityRuleType.EducationLevel)]
    [InlineData(EligibilityRuleType.HouseholdIncomeBand)]
    [InlineData(EligibilityRuleType.FieldOfStudy)]
    public void Unassessed_candidate_data_is_unknown_never_not_met(EligibilityRuleType ruleType)
    {
        // CLAUDE.md rule 2's spirit extended to eligibility: missing data is Unknown, never a failure.
        var rule = new EligibilityRule(Guid.NewGuid(), ruleType, EligibilityOperator.Equals, "Whatever", true, "");

        Assert.Equal(EligibilityResult.Unknown, EligibilityRuleEvaluator.Evaluate(rule, BlankCandidate));
    }

    [Fact]
    public void AgeRange_with_no_date_of_birth_is_unknown()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.AgeRange, EligibilityOperator.Between, "18-25", true, "");

        Assert.Equal(EligibilityResult.Unknown, EligibilityRuleEvaluator.Evaluate(rule, BlankCandidate));
    }

    [Theory]
    [InlineData(20, EligibilityResult.Met)]
    [InlineData(17, EligibilityResult.NotMet)]
    [InlineData(26, EligibilityResult.NotMet)]
    [InlineData(18, EligibilityResult.Met)]
    [InlineData(25, EligibilityResult.Met)]
    public void AgeRange_is_evaluated_inclusively(int ageYears, EligibilityResult expected)
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.AgeRange, EligibilityOperator.Between, "18-25", true, "");
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-ageYears).AddDays(-1));
        var candidate = BlankCandidate with { DateOfBirth = dob };

        Assert.Equal(expected, EligibilityRuleEvaluator.Evaluate(rule, candidate));
    }

    [Fact]
    public void In_rule_matches_any_listed_value()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.HouseholdIncomeBand, EligibilityOperator.In, "Low,Medium", false, "");
        var candidate = BlankCandidate with { HouseholdIncomeBand = "Medium" };

        Assert.Equal(EligibilityResult.Met, EligibilityRuleEvaluator.Evaluate(rule, candidate));
    }

    [Fact]
    public void In_rule_does_not_match_unlisted_value()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.HouseholdIncomeBand, EligibilityOperator.In, "Low,Medium", false, "");
        var candidate = BlankCandidate with { HouseholdIncomeBand = "High" };

        Assert.Equal(EligibilityResult.NotMet, EligibilityRuleEvaluator.Evaluate(rule, candidate));
    }

    [Fact]
    public void Any_sentinel_value_is_always_met_even_with_no_candidate_data()
    {
        var rule = new EligibilityRule(Guid.NewGuid(), EligibilityRuleType.EducationLevel, EligibilityOperator.Equals, "Any", false, "");

        Assert.Equal(EligibilityResult.Met, EligibilityRuleEvaluator.Evaluate(rule, BlankCandidate));
    }

    [Fact]
    public void Summary_is_blocked_only_by_a_hard_filter_rule_that_is_not_met()
    {
        var opportunity = new Opportunity(Guid.NewGuid(), "Role", "Desc", OpportunityType.Bursary, null, null);
        opportunity.AddEligibilityRule(EligibilityRuleType.Province, EligibilityOperator.Equals, "Gauteng", isHardFilter: true, "Must be in Gauteng.");
        opportunity.AddEligibilityRule(EligibilityRuleType.HouseholdIncomeBand, EligibilityOperator.In, "Low", isHardFilter: false, "Preference, not a requirement.");

        var ineligibleCandidate = BlankCandidate with { Province = "Limpopo", HouseholdIncomeBand = "High" };
        var summary = OpportunityEligibilitySummary.Evaluate(opportunity, ineligibleCandidate);

        Assert.True(summary.IsBlockedByHardFilter);
    }

    [Fact]
    public void Summary_is_not_blocked_by_a_soft_rule_that_is_not_met()
    {
        var opportunity = new Opportunity(Guid.NewGuid(), "Role", "Desc", OpportunityType.Bursary, null, null);
        opportunity.AddEligibilityRule(EligibilityRuleType.HouseholdIncomeBand, EligibilityOperator.In, "Low", isHardFilter: false, "Preference, not a requirement.");

        var candidate = BlankCandidate with { HouseholdIncomeBand = "High" };
        var summary = OpportunityEligibilitySummary.Evaluate(opportunity, candidate);

        Assert.False(summary.IsBlockedByHardFilter);
    }

    [Fact]
    public void Summary_is_not_blocked_when_hard_filter_is_unknown()
    {
        var opportunity = new Opportunity(Guid.NewGuid(), "Role", "Desc", OpportunityType.Bursary, null, null);
        opportunity.AddEligibilityRule(EligibilityRuleType.Province, EligibilityOperator.Equals, "Gauteng", isHardFilter: true, "Must be in Gauteng.");

        var summary = OpportunityEligibilitySummary.Evaluate(opportunity, BlankCandidate);

        Assert.False(summary.IsBlockedByHardFilter);
        Assert.Equal(EligibilityResult.Unknown, summary.Rules.Single().Result);
    }
}
