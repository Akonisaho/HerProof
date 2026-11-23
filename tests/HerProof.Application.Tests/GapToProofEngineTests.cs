using HerProof.Application.Competencies;
using HerProof.Domain.Competencies;
using HerProof.Domain.Opportunities;
using Xunit;

namespace HerProof.Application.Tests;

public class GapToProofEngineTests
{
    private static Opportunity MakeOpportunity(Guid competencyId, int requiredLevel)
    {
        var opportunity = new Opportunity(Guid.NewGuid(), "Role", "Desc", OpportunityType.Job, null, null);
        opportunity.RequireCompetency(competencyId, requiredLevel, isMandatory: true);
        return opportunity;
    }

    [Fact]
    public void No_evidence_never_produces_a_demonstrated_level_of_zero()
    {
        var competencyId = Guid.NewGuid();
        var opportunity = MakeOpportunity(competencyId, requiredLevel: 2);

        var report = GapToProofEngine.Generate(
            opportunity,
            Guid.NewGuid(),
            candidateCompetenciesByCompetencyId: new Dictionary<Guid, CandidateCompetency>(),
            competencyNames: new Dictionary<Guid, string> { [competencyId] = "C#" },
            learningResourceByCompetencyId: new Dictionary<Guid, LearningResource>(),
            nowUtc: DateTime.UtcNow);

        var item = Assert.Single(report.Items);
        Assert.Equal(EvidenceState.NoEvidence, item.EvidenceState);
        Assert.Null(item.DemonstratedLevel);
        Assert.NotEqual(0, item.DemonstratedLevel ?? -1); // sentinel: -1 proves it's genuinely null, not defaulted
    }

    [Fact]
    public void Claimed_but_unassessed_competency_is_never_a_demonstrated_level_of_zero()
    {
        var competencyId = Guid.NewGuid();
        var opportunity = MakeOpportunity(competencyId, requiredLevel: 2);
        var candidateCompetency = new CandidateCompetency(Guid.NewGuid(), competencyId, declaredLevel: 3, DateTime.UtcNow);
        // Status defaults to Claimed — explicitly not assessed.

        var report = GapToProofEngine.Generate(
            opportunity,
            Guid.NewGuid(),
            new Dictionary<Guid, CandidateCompetency> { [competencyId] = candidateCompetency },
            new Dictionary<Guid, string> { [competencyId] = "C#" },
            new Dictionary<Guid, LearningResource>(),
            DateTime.UtcNow);

        var item = Assert.Single(report.Items);
        Assert.Equal(EvidenceState.ClaimedNotAssessed, item.EvidenceState);
        Assert.Null(item.DemonstratedLevel);
    }

    [Fact]
    public void Not_demonstrated_is_distinct_from_no_evidence_and_still_not_a_zero()
    {
        var competencyId = Guid.NewGuid();
        var opportunity = MakeOpportunity(competencyId, requiredLevel: 2);
        var candidateCompetency = new CandidateCompetency(Guid.NewGuid(), competencyId, declaredLevel: 2, DateTime.UtcNow);
        candidateCompetency.ApplyAssessorDecision(CandidateCompetencyStatus.NotDemonstrated, finalLevel: null, DateTime.UtcNow);

        var report = GapToProofEngine.Generate(
            opportunity,
            Guid.NewGuid(),
            new Dictionary<Guid, CandidateCompetency> { [competencyId] = candidateCompetency },
            new Dictionary<Guid, string> { [competencyId] = "C#" },
            new Dictionary<Guid, LearningResource>(),
            DateTime.UtcNow);

        var item = Assert.Single(report.Items);
        Assert.Equal(EvidenceState.NotDemonstrated, item.EvidenceState);
        Assert.Null(item.DemonstratedLevel);
        Assert.NotEqual(EvidenceState.NoEvidence, item.EvidenceState);
    }

    [Fact]
    public void Verified_competency_reports_its_actual_demonstrated_level()
    {
        var competencyId = Guid.NewGuid();
        var opportunity = MakeOpportunity(competencyId, requiredLevel: 2);
        var candidateCompetency = new CandidateCompetency(Guid.NewGuid(), competencyId, declaredLevel: 3, DateTime.UtcNow);
        candidateCompetency.ApplyAssessorDecision(CandidateCompetencyStatus.Verified, finalLevel: 3, DateTime.UtcNow);

        var report = GapToProofEngine.Generate(
            opportunity,
            Guid.NewGuid(),
            new Dictionary<Guid, CandidateCompetency> { [competencyId] = candidateCompetency },
            new Dictionary<Guid, string> { [competencyId] = "C#" },
            new Dictionary<Guid, LearningResource>(),
            DateTime.UtcNow);

        var item = Assert.Single(report.Items);
        Assert.Equal(EvidenceState.Demonstrated, item.EvidenceState);
        Assert.Equal(3, item.DemonstratedLevel);
    }

    [Fact]
    public void Explanation_never_contains_the_literal_word_zero_for_unassessed_states()
    {
        var competencyId = Guid.NewGuid();
        var opportunity = MakeOpportunity(competencyId, requiredLevel: 2);

        var report = GapToProofEngine.Generate(
            opportunity,
            Guid.NewGuid(),
            new Dictionary<Guid, CandidateCompetency>(),
            new Dictionary<Guid, string> { [competencyId] = "C#" },
            new Dictionary<Guid, LearningResource>(),
            DateTime.UtcNow);

        var item = Assert.Single(report.Items);
        Assert.DoesNotContain("zero", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" 0 ", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Explanation_recommends_the_linked_learning_resource_when_one_exists()
    {
        var competencyId = Guid.NewGuid();
        var opportunity = MakeOpportunity(competencyId, requiredLevel: 2);
        var resource = new LearningResource(competencyId, "Intro to C#", "https://example.test/csharp", "Free", 1);

        var report = GapToProofEngine.Generate(
            opportunity,
            Guid.NewGuid(),
            new Dictionary<Guid, CandidateCompetency>(),
            new Dictionary<Guid, string> { [competencyId] = "C#" },
            new Dictionary<Guid, LearningResource> { [competencyId] = resource },
            DateTime.UtcNow);

        var item = Assert.Single(report.Items);
        Assert.Equal(resource.Id, item.LearningResourceId);
        Assert.Contains("Intro to C#", item.Explanation);
    }

    [Fact]
    public void Gap_report_item_constructor_rejects_a_demonstrated_level_without_demonstrated_state()
    {
        // Defence in depth at the domain layer itself, independent of the engine (docs/05 P05).
        var report = new GapReport(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            report.AddItem(Guid.NewGuid(), 2, demonstratedLevel: 1, EvidenceState.ClaimedNotAssessed, "explanation", null));
    }
}
