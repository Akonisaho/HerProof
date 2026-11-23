using HerProof.Domain.Opportunities;

namespace HerProof.Application.Opportunities;

public record EligibilityRuleEvaluation(Guid RuleId, EligibilityRuleType RuleType, bool IsHardFilter, string Explanation, EligibilityResult Result);

/// <summary>Per-rule results plus one overall decision. Unknown never blocks — only a hard-filter
/// rule that evaluates NotMet does (docs/02: eligibility results are Met/NotMet/Unknown; no opaque
/// rejection, docs/05 P16).</summary>
public record OpportunityEligibilitySummary(IReadOnlyList<EligibilityRuleEvaluation> Rules)
{
    public bool IsBlockedByHardFilter => Rules.Any(r => r.IsHardFilter && r.Result == EligibilityResult.NotMet);

    public static OpportunityEligibilitySummary Evaluate(Opportunity opportunity, CandidateEligibilityData candidate)
    {
        var evaluations = opportunity.EligibilityRules
            .Select(rule => new EligibilityRuleEvaluation(
                rule.Id,
                rule.RuleType,
                rule.IsHardFilter,
                rule.Explanation,
                EligibilityRuleEvaluator.Evaluate(rule, candidate)))
            .ToList();

        return new OpportunityEligibilitySummary(evaluations);
    }
}
