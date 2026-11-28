using HerProof.Domain.Assessments;
using HerProof.Domain.Competencies;

namespace HerProof.Web.Models.Learning;

public class CareerRoadmapStep
{
    public required int StepNumber { get; init; }
    public required Guid CompetencyId { get; init; }
    public required string CompetencyName { get; init; }
    public required int TargetLevel { get; init; }
    public required CandidateCompetencyStatus? CurrentStatus { get; init; }
    public required IReadOnlyList<(string Title, string Url, string CostType)> Resources { get; init; }

    /// <summary>True once the candidate has a RoadmapStepCompletion row for this step - the sandbox
    /// actually built her submission and every test passed (RoadmapStepEvaluator), never a human
    /// reviewer and never fabricated.</summary>
    public required bool IsCompleted { get; init; }

    /// <summary>True until every earlier step is completed - a locked step shows no interactive CTA
    /// at all, so she genuinely cannot skip ahead from this page.</summary>
    public required bool IsLocked { get; init; }

    /// <summary>Published assessments that cover this step's competency - the same real, timed,
    /// Electron-monitored practical assessment flow used everywhere else in the app, just reused here.
    /// AlsoCovers names every OTHER competency on this same roadmap the assessment covers, so a step
    /// like Problem-solving honestly shows that its only real assessment is a shared practical that
    /// also tests OOP and C# - never presented as if it were a dedicated Problem-solving test.</summary>
    public required IReadOnlyList<(Guid Id, string Title, int DurationMinutes, IReadOnlyList<string> AlsoCovers)> AvailableAssessments { get; init; }
}

public class CareerRoadmapViewModel
{
    public required IReadOnlyList<string> AvailableGoals { get; init; }
    public string? SelectedGoal { get; init; }
    public IReadOnlyList<CareerRoadmapStep>? Steps { get; init; }

    /// <summary>The language this goal is tagged with, if any (CareerRoadmaps.Goals) - drives the
    /// capstone card's wording ("build it in C#", "build it in Java") so it never tells her to use a
    /// language this roadmap never actually covered.</summary>
    public AssessmentLanguage? GoalLanguage { get; init; }

    /// <summary>Only true once every step above has IsCompleted - this is what unlocks the capstone
    /// card pointing her at the real, human-Assessor-reviewed verification pipeline.</summary>
    public bool AllStepsCompleted => Steps is { Count: > 0 } && Steps.All(s => s.IsCompleted);

    /// <summary>A single shared practical can complete several steps in one submission (e.g. Core C#
    /// Practical satisfies Problem-solving, OOP and C# together) - every one of them, not just one.</summary>
    public IReadOnlyList<string> JustCompletedCompetencyNames { get; init; } = [];
}
