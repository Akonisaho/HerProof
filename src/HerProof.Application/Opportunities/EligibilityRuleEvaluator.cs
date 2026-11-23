using HerProof.Domain.Opportunities;

namespace HerProof.Application.Opportunities;

/// <summary>Pure logic, heavily tested (docs/05 P04): evaluates one eligibility rule against a
/// candidate's self-declared data to Met, NotMet or Unknown. Never scores accent, face or any
/// biometric signal (CLAUDE.md rule 4) — these are declared facts the candidate typed in, nothing more.</summary>
public static class EligibilityRuleEvaluator
{
    /// <summary>A rule value of "Any" (case-insensitive) always evaluates Met: used for rules that
    /// exist to be shown to the candidate as a condition, not to filter anyone out.</summary>
    private const string AnyValueSentinel = "Any";

    public static EligibilityResult Evaluate(EligibilityRule rule, CandidateEligibilityData candidate)
    {
        if (string.Equals(rule.Value, AnyValueSentinel, StringComparison.OrdinalIgnoreCase))
        {
            return EligibilityResult.Met;
        }

        var candidateValue = GetCandidateValue(rule.RuleType, candidate);

        return rule.RuleType == EligibilityRuleType.AgeRange
            ? EvaluateAgeRange(rule, candidate.DateOfBirth)
            : EvaluateStringRule(rule, candidateValue);
    }

    private static string? GetCandidateValue(EligibilityRuleType ruleType, CandidateEligibilityData candidate) => ruleType switch
    {
        EligibilityRuleType.Province => candidate.Province,
        EligibilityRuleType.GenderDeclared => candidate.GenderDeclared,
        EligibilityRuleType.EducationLevel => candidate.EducationLevel,
        EligibilityRuleType.HouseholdIncomeBand => candidate.HouseholdIncomeBand,
        EligibilityRuleType.FieldOfStudy => candidate.FieldOfStudy,
        EligibilityRuleType.AgeRange => null,
        _ => throw new ArgumentOutOfRangeException(nameof(ruleType), ruleType, "Unknown eligibility rule type."),
    };

    private static EligibilityResult EvaluateStringRule(EligibilityRule rule, string? candidateValue)
    {
        if (string.IsNullOrWhiteSpace(candidateValue))
        {
            return EligibilityResult.Unknown;
        }

        return rule.Operator switch
        {
            EligibilityOperator.Equals => string.Equals(candidateValue, rule.Value, StringComparison.OrdinalIgnoreCase)
                ? EligibilityResult.Met
                : EligibilityResult.NotMet,
            EligibilityOperator.In => rule.Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(v => string.Equals(v, candidateValue, StringComparison.OrdinalIgnoreCase))
                ? EligibilityResult.Met
                : EligibilityResult.NotMet,
            _ => throw new NotSupportedException($"Operator {rule.Operator} is not supported for rule type {rule.RuleType}."),
        };
    }

    private static EligibilityResult EvaluateAgeRange(EligibilityRule rule, DateOnly? dateOfBirth)
    {
        if (dateOfBirth is null)
        {
            return EligibilityResult.Unknown;
        }

        if (rule.Operator != EligibilityOperator.Between)
        {
            throw new NotSupportedException($"Operator {rule.Operator} is not supported for AgeRange; use Between with Value \"min-max\".");
        }

        var parts = rule.Value.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var min) || !int.TryParse(parts[1], out var max))
        {
            throw new FormatException($"AgeRange rule value \"{rule.Value}\" is not in \"min-max\" format.");
        }

        var age = CalculateAge(dateOfBirth.Value, DateOnly.FromDateTime(DateTime.UtcNow));
        return age >= min && age <= max ? EligibilityResult.Met : EligibilityResult.NotMet;
    }

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
