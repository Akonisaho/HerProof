using HerProof.Domain.Common;

namespace HerProof.Domain.Opportunities;

public enum OpportunityType
{
    Job,
    Internship,
    Learnership,
    Bursary,
    Hackathon,
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

    /// <summary>Both null means the recruiter chose not to disclose a salary ("N/A"), shown as such
    /// rather than left blank. Equal, non-null min/max is a specific amount; a differing min/max is a
    /// range. Never used for AI scoring or eligibility — display and candidate-side filtering only.</summary>
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }

    /// <summary>Years of relevant experience the role asks for — informational and filterable, not an
    /// eligibility hard-filter (a candidate can still apply; CLAUDE.md "never a zero").</summary>
    public int? ExperienceYearsRequired { get; private set; }

    public IReadOnlyCollection<OpportunityCompetency> RequiredCompetencies => _requiredCompetencies;
    public IReadOnlyCollection<EligibilityRule> EligibilityRules => _eligibilityRules;

    /// <summary>The practical assessment a candidate is meant to take for this specific role, if the
    /// recruiter/partner attached one - distinct from the generic competency-matched assessment list,
    /// which still applies when this is null.</summary>
    public Guid? LinkedAssessmentId { get; private set; }

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

    public void SetCompensationAndExperience(decimal? salaryMin, decimal? salaryMax, int? experienceYearsRequired)
    {
        SalaryMin = salaryMin;
        SalaryMax = salaryMax;
        ExperienceYearsRequired = experienceYearsRequired;
    }

    public void Publish() => Status = OpportunityStatus.Published;

    public void Close() => Status = OpportunityStatus.Closed;

    public void LinkAssessment(Guid assessmentId) => LinkedAssessmentId = assessmentId;

    public void UnlinkAssessment() => LinkedAssessmentId = null;

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
