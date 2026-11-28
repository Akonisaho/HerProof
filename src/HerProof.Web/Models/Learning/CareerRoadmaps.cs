using HerProof.Domain.Assessments;

namespace HerProof.Web.Models.Learning;

/// <summary>Hand-curated "I don't know what to do yet" roadmaps for candidates with no specific
/// opportunity in mind - an ordered skill path per goal, resolved at request time against real
/// Competency rows and real LearningResource links (never invented URLs). Deliberately static data,
/// not a new database table: a handful of goals is plenty for a hackathon MVP, and moving this to an
/// admin-editable table is a safe, additive change later if it's ever needed.</summary>
public static class CareerRoadmaps
{
    /// <summary>A goal's Language, when set, is the ONLY language whose practical assessments this
    /// roadmap ever offers - without this, a competency like Problem-solving that's genuinely covered
    /// by both Core C# Practical and Core Java Practical would show both "Take" buttons on every
    /// roadmap that includes it, even the Java-only one. Null means the goal isn't tied to one
    /// programming language (e.g. Operations/Project Coordinator) and shows whatever real assessment
    /// covers each step, regardless of language.</summary>
    public static readonly IReadOnlyDictionary<string, (AssessmentLanguage? Language, IReadOnlyList<(string CompetencyName, int TargetLevel)> Steps)> Goals =
        new Dictionary<string, (AssessmentLanguage?, IReadOnlyList<(string, int)>)>
        {
            ["Software Developer (C#/.NET)"] = (AssessmentLanguage.CSharp, new List<(string, int)>
            {
                ("Problem-solving", 1),
                ("Git", 1),
                ("OOP", 2),
                ("C#", 2),
                ("Testing", 1),
                ("API development", 2),
                ("ASP.NET Core", 2),
            }),
            ["Software Developer (Java)"] = (AssessmentLanguage.Java, new List<(string, int)>
            {
                ("Problem-solving", 1),
                ("OOP", 2),
                ("Java", 2),
            }),
            ["Operations / Project Coordinator"] = (null, new List<(string, int)>
            {
                ("Problem-solving", 1),
                ("Scheduling and planning", 1),
                ("Resource management", 1),
                ("Team coordination", 2),
                ("Project coordination", 2),
                ("Business operations", 1),
            }),
            ["Data Engineer"] = (AssessmentLanguage.CSharp, new List<(string, int)>
            {
                ("Problem-solving", 1),
                ("Git", 1),
                ("SQL", 2),
                ("C#", 1),
                ("Error handling", 1),
                ("Testing", 1),
            }),
            ["Data Analyst"] = (null, new List<(string, int)>
            {
                ("Problem-solving", 1),
                ("SQL", 2),
                ("Testing", 1),
                ("Business operations", 1),
            }),
            ["System Analyst"] = (null, new List<(string, int)>
            {
                ("Problem-solving", 1),
                ("Business operations", 1),
                ("SQL", 1),
                ("Project coordination", 1),
                ("Testing", 1),
            }),
        };
}
