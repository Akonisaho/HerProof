using HerProof.Domain.Competencies;
using HerProof.Domain.Opportunities;

namespace HerProof.Application.Competencies;

/// <summary>Pure, heavily tested logic (docs/05 P05). CLAUDE.md rule 2, non-negotiable: an
/// unassessed competency is NEVER treated as level 0. DemonstratedLevel is null — not 0 — for every
/// evidence_state except Demonstrated, and no code path here may default it to zero.</summary>
public static class GapToProofEngine
{
    public static GapReport Generate(
        Opportunity opportunity,
        Guid candidateId,
        IReadOnlyDictionary<Guid, CandidateCompetency> candidateCompetenciesByCompetencyId,
        IReadOnlyDictionary<Guid, string> competencyNames,
        IReadOnlyDictionary<Guid, LearningResource> learningResourceByCompetencyId,
        DateTime nowUtc)
    {
        var report = new GapReport(candidateId, opportunity.Id, nowUtc);

        foreach (var requirement in opportunity.RequiredCompetencies)
        {
            candidateCompetenciesByCompetencyId.TryGetValue(requirement.CompetencyId, out var candidateCompetency);
            var (evidenceState, demonstratedLevel) = Classify(candidateCompetency);
            var competencyName = competencyNames.GetValueOrDefault(requirement.CompetencyId, "This competency");
            learningResourceByCompetencyId.TryGetValue(requirement.CompetencyId, out var learningResource);

            var explanation = BuildExplanation(competencyName, requirement.RequiredLevel, evidenceState, demonstratedLevel, learningResource);

            report.AddItem(requirement.CompetencyId, requirement.RequiredLevel, demonstratedLevel, evidenceState, explanation, learningResource?.Id);
        }

        return report;
    }

    /// <summary>The only place evidence_state and demonstrated_level are decided. Keep this the
    /// single source of truth: nothing else in the codebase should infer evidence_state from status.</summary>
    private static (EvidenceState State, int? DemonstratedLevel) Classify(CandidateCompetency? candidateCompetency)
    {
        if (candidateCompetency is null)
        {
            return (EvidenceState.NoEvidence, null);
        }

        return candidateCompetency.Status switch
        {
            CandidateCompetencyStatus.Verified => (EvidenceState.Demonstrated, candidateCompetency.VerifiedLevel),
            CandidateCompetencyStatus.NotDemonstrated => (EvidenceState.NotDemonstrated, null),
            _ => (EvidenceState.ClaimedNotAssessed, null),
        };
    }

    private static string BuildExplanation(string competencyName, int requiredLevel, EvidenceState state, int? demonstratedLevel, LearningResource? learningResource)
    {
        var why = $"{competencyName} is required at level {requiredLevel} for this opportunity.";

        var status = state switch
        {
            EvidenceState.NoEvidence => "You haven't added any evidence for this competency yet.",
            EvidenceState.ClaimedNotAssessed => "You've claimed this competency, but it hasn't been assessed yet — this is not the same as a zero.",
            EvidenceState.Demonstrated => $"You've demonstrated this at level {demonstratedLevel}.",
            EvidenceState.NotDemonstrated => "This was assessed and not demonstrated through the evidence provided.",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

        var recommendation = state == EvidenceState.Demonstrated && demonstratedLevel >= requiredLevel
            ? "No action needed for this competency."
            : learningResource is not null
                ? $"Recommended: {learningResource.Title}."
                : "Request an assessment to close this gap.";

        return $"{why} {status} {recommendation}";
    }
}
