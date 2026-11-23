using HerProof.Application.Experience;
using HerProof.Domain.Competencies;
using HerProof.Domain.Experience;
using Xunit;

namespace HerProof.Application.Tests;

public class ExperienceCompetencyMapperTests
{
    private static readonly Competency FinancialManagement = new("Financial management", CompetencyCategory.NonTraditional, "v1");
    private static readonly Competency SchedulingAndPlanning = new("Scheduling and planning", CompetencyCategory.NonTraditional, "v1");
    private static readonly Competency CSharp = new("C#", CompetencyCategory.Software, "v1");

    [Fact]
    public void Lerato_household_budgeting_example_suggests_financial_management()
    {
        // The exact scenario from docs/00-PITCH.md section 3, step 1.
        var record = new ExperienceRecord(
            Guid.NewGuid(), ExperienceCategory.Household, "Managing the household",
            "Managed a household budget for six years, planning spending and savings for the family.",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-6)), null);

        var suggestions = ExperienceCompetencyMapper.Suggest(record, [FinancialManagement, SchedulingAndPlanning, CSharp]);

        Assert.Contains(FinancialManagement.Id, suggestions);
        Assert.Contains(SchedulingAndPlanning.Id, suggestions);
    }

    [Fact]
    public void Software_competencies_are_never_suggested_by_the_keyword_mapper()
    {
        var record = new ExperienceRecord(Guid.NewGuid(), ExperienceCategory.Freelance, "Freelance coding",
            "Wrote C# code for a small business's budget tracking app.", DateOnly.FromDateTime(DateTime.UtcNow), null);

        var suggestions = ExperienceCompetencyMapper.Suggest(record, [FinancialManagement, CSharp]);

        Assert.DoesNotContain(CSharp.Id, suggestions);
    }

    [Fact]
    public void Unrelated_experience_suggests_nothing()
    {
        var record = new ExperienceRecord(Guid.NewGuid(), ExperienceCategory.Other, "Hobby",
            "Enjoys painting landscapes on weekends.", DateOnly.FromDateTime(DateTime.UtcNow), null);

        var suggestions = ExperienceCompetencyMapper.Suggest(record, [FinancialManagement, SchedulingAndPlanning]);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Suggested_mapping_starts_in_suggested_status_not_confirmed()
    {
        var record = new ExperienceRecord(Guid.NewGuid(), ExperienceCategory.Household, "Title", "budget", DateOnly.FromDateTime(DateTime.UtcNow), null);
        var mapping = record.SuggestCompetency(FinancialManagement.Id);

        Assert.Equal(ExperienceCompetencyMappingStatus.Suggested, mapping.MappingStatus);
    }

    [Fact]
    public void Confirming_a_mapping_sets_confirmed_status()
    {
        var record = new ExperienceRecord(Guid.NewGuid(), ExperienceCategory.Household, "Title", "budget", DateOnly.FromDateTime(DateTime.UtcNow), null);
        var mapping = record.SuggestCompetency(FinancialManagement.Id);

        mapping.Confirm();

        Assert.Equal(ExperienceCompetencyMappingStatus.Confirmed, mapping.MappingStatus);
    }

    [Fact]
    public void Rejecting_a_mapping_sets_rejected_status()
    {
        var record = new ExperienceRecord(Guid.NewGuid(), ExperienceCategory.Household, "Title", "budget", DateOnly.FromDateTime(DateTime.UtcNow), null);
        var mapping = record.SuggestCompetency(FinancialManagement.Id);

        mapping.Reject();

        Assert.Equal(ExperienceCompetencyMappingStatus.Rejected, mapping.MappingStatus);
    }
}
