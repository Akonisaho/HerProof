using HerProof.Domain.Competencies;
using HerProof.Domain.Experience;

namespace HerProof.Application.Experience;

/// <summary>Curated keyword/rule mapper (docs/05 P06): proposes non-traditional competencies from an
/// experience record's own words. Deliberately simple for the hackathon — an LLM-backed mapper behind
/// IAIService is the documented optional upgrade, not a requirement. Every result is a *suggestion*:
/// mappings are never awards (CLAUDE.md working agreement), and the same assessment standard applies
/// regardless of where a competency was claimed from.</summary>
public static class ExperienceCompetencyMapper
{
    private static readonly Dictionary<string, string[]> KeywordsByCompetencyName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Financial management"] = ["budget", "budgeting", "finance", "financial", "money", "expenses", "accounting", "savings"],
        ["Scheduling and planning"] = ["schedule", "scheduling", "planning", "planned", "calendar", "organising", "organizing", "time management"],
        ["Project coordination"] = ["coordinate", "coordinated", "coordinating", "project", "organised an event", "organized an event"],
        ["Business operations"] = ["business", "operations", "ran a shop", "small business", "stall", "spaza"],
        ["Resource management"] = ["resources", "inventory", "stock", "supplies", "procurement", "allocating"],
        ["Team coordination"] = ["team", "volunteers", "committee", "group of people", "led a group", "coordinating people"],
    };

    /// <summary>Matches on the record's title and description against each non-traditional competency's
    /// keyword list. Returns the matching competencies' ids, each to be added as a Suggested mapping.</summary>
    public static IReadOnlyList<Guid> Suggest(ExperienceRecord experienceRecord, IReadOnlyCollection<Competency> nonTraditionalCompetencies)
    {
        var haystack = $"{experienceRecord.Title} {experienceRecord.Description}";

        return nonTraditionalCompetencies
            .Where(c => c.Category == CompetencyCategory.NonTraditional)
            .Where(c => KeywordsByCompetencyName.TryGetValue(c.Name, out var keywords)
                && keywords.Any(k => haystack.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.Id)
            .ToList();
    }
}
