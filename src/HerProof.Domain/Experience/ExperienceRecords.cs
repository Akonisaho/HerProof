using HerProof.Domain.Common;

namespace HerProof.Domain.Experience;

public enum ExperienceCategory
{
    Caregiving,
    Household,
    Community,
    Freelance,
    Volunteer,
    Other,
}

/// <summary>Beyond Employment (CLAUDE.md, docs/05 P06). Mappings are never awards: the same
/// assessment standard applies regardless of where a competency was acquired.</summary>
public class ExperienceRecord : Entity
{
    private readonly List<ExperienceCompetency> _suggestedCompetencies = [];

    public Guid CandidateId { get; init; }
    public ExperienceCategory Category { get; init; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    public IReadOnlyCollection<ExperienceCompetency> SuggestedCompetencies => _suggestedCompetencies;

    private ExperienceRecord()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    public ExperienceRecord(Guid candidateId, ExperienceCategory category, string title, string description, DateOnly startDate, DateOnly? endDate)
    {
        CandidateId = candidateId;
        Category = category;
        Title = title;
        Description = description;
        StartDate = startDate;
        EndDate = endDate;
    }

    public ExperienceCompetency SuggestCompetency(Guid competencyId)
    {
        var mapping = new ExperienceCompetency(Id, competencyId);
        _suggestedCompetencies.Add(mapping);
        return mapping;
    }
}

public enum ExperienceCompetencyMappingStatus
{
    Suggested,
    Confirmed,
    Rejected,
}

public class ExperienceCompetency : Entity
{
    public Guid ExperienceRecordId { get; init; }
    public Guid CompetencyId { get; init; }
    public ExperienceCompetencyMappingStatus MappingStatus { get; private set; } = ExperienceCompetencyMappingStatus.Suggested;

    private ExperienceCompetency()
    {
    }

    public ExperienceCompetency(Guid experienceRecordId, Guid competencyId)
    {
        ExperienceRecordId = experienceRecordId;
        CompetencyId = competencyId;
    }

    public void Confirm() => MappingStatus = ExperienceCompetencyMappingStatus.Confirmed;

    public void Reject() => MappingStatus = ExperienceCompetencyMappingStatus.Rejected;
}
