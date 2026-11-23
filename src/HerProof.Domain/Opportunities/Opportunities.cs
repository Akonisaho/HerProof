using HerProof.Domain.Common;

namespace HerProof.Domain.Opportunities;

public enum OpportunityType
{
    Job,
    Internship,
    Learnership,
    Bursary,
}

public enum OpportunityStatus
{
    Draft,
    Published,
    Closed,
}

public class Opportunity : Entity
{
    private readonly List<OpportunityCompetency> _requiredCompetencies = [];
    private readonly List<EligibilityRule> _eligibilityRules = [];

    public Guid OrganisationId { get; init; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public OpportunityType OpportunityType { get; private set; }
    public string? Location { get; private set; }
    public DateTime? DeadlineUtc { get; private set; }
    public OpportunityStatus Status { get; private set; } = OpportunityStatus.Draft;

    public IReadOnlyCollection<OpportunityCompetency> RequiredCompetencies => _requiredCompetencies;
    public IReadOnlyCollection<EligibilityRule> EligibilityRules => _eligibilityRules;

    private Opportunity()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    public Opportunity(Guid organisationId, string title, string description, OpportunityType opportunityType, string? location, DateTime? deadlineUtc)
    {
        OrganisationId = organisationId;
        Title = title;
        Description = description;
        OpportunityType = opportunityType;
        Location = location;
        DeadlineUtc = deadlineUtc;
    }

    public void Publish() => Status = OpportunityStatus.Published;

    public void Close() => Status = OpportunityStatus.Closed;

    public OpportunityCompetency RequireCompetency(Guid competencyId, int requiredLevel, bool isMandatory)
    {
        var requirement = new OpportunityCompetency(Id, competencyId, requiredLevel, isMandatory);
        _requiredCompetencies.Add(requirement);
        return requirement;
    }

    public EligibilityRule AddEligibilityRule(EligibilityRuleType ruleType, EligibilityOperator ruleOperator, string value, bool isHardFilter, string explanation)
    {
        var rule = new EligibilityRule(Id, ruleType, ruleOperator, value, isHardFilter, explanation);
        _eligibilityRules.Add(rule);
        return rule;
    }
}

public class OpportunityCompetency : Entity
{
    public Guid OpportunityId { get; init; }
    public Guid CompetencyId { get; init; }
    public int RequiredLevel { get; init; }
    public bool IsMandatory { get; init; }

    private OpportunityCompetency()
    {
    }

    public OpportunityCompetency(Guid opportunityId, Guid competencyId, int requiredLevel, bool isMandatory)
    {
        OpportunityId = opportunityId;
        CompetencyId = competencyId;
        RequiredLevel = requiredLevel;
        IsMandatory = isMandatory;
    }
}

/// <summary>The Gender rule exists only as a provider-configured, candidate-visible eligibility
/// criterion (e.g. a women-only bursary). It is never used in AI scoring (CLAUDE.md rule 4, P04).</summary>
public enum EligibilityRuleType
{
    Province,
    GenderDeclared,
    EducationLevel,
    HouseholdIncomeBand,
    FieldOfStudy,
    AgeRange,
}

public enum EligibilityOperator
{
    Equals,
    In,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Between,
}

public enum EligibilityResult
{
    Met,
    NotMet,
    Unknown,
}

public class EligibilityRule : Entity
{
    public Guid OpportunityId { get; init; }
    public EligibilityRuleType RuleType { get; init; }
    public EligibilityOperator Operator { get; init; }
    public string Value { get; init; }
    public bool IsHardFilter { get; init; }
    public string Explanation { get; init; }

    private EligibilityRule()
    {
        Value = string.Empty;
        Explanation = string.Empty;
    }

    public EligibilityRule(Guid opportunityId, EligibilityRuleType ruleType, EligibilityOperator ruleOperator, string value, bool isHardFilter, string explanation)
    {
        OpportunityId = opportunityId;
        RuleType = ruleType;
        Operator = ruleOperator;
        Value = value;
        IsHardFilter = isHardFilter;
        Explanation = explanation;
    }
}

public enum CandidateApplicationStatus
{
    Submitted,
    Withdrawn,
    Reviewed,
}

public class CandidateApplication : Entity
{
    public Guid OpportunityId { get; init; }
    public Guid CandidateId { get; init; }
    public CandidateApplicationStatus Status { get; private set; } = CandidateApplicationStatus.Submitted;

    /// <summary>Passport credential ids the candidate chose to attach, e.g. to a bursary application.</summary>
    public string SharedCredentialsJson { get; private set; }

    public DateTime SubmittedAtUtc { get; init; }

    private CandidateApplication()
    {
        SharedCredentialsJson = "[]";
    }

    public CandidateApplication(Guid opportunityId, Guid candidateId, string sharedCredentialsJson, DateTime submittedAtUtc)
    {
        OpportunityId = opportunityId;
        CandidateId = candidateId;
        SharedCredentialsJson = sharedCredentialsJson;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void Withdraw() => Status = CandidateApplicationStatus.Withdrawn;

    public void MarkReviewed() => Status = CandidateApplicationStatus.Reviewed;
}
