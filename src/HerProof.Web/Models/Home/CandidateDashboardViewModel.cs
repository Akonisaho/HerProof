using HerProof.Domain.Interviews;
using HerProof.Domain.Opportunities;

namespace HerProof.Web.Models.Home;

public class CandidateDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;

    public string? PassportNumber { get; set; }

    public IReadOnlyList<(string CompetencyName, int? Level)> VerifiedCredentials { get; set; } = [];

    public int UnverifiedCompetencyCount { get; set; }

    public int ApplicationCount { get; set; }

    /// <summary>A share's raw token is shown exactly once, at creation, and never stored (docs/02) —
    /// so a later page load can say an active share exists, but can't regenerate its QR here. The
    /// dashboard links to My Passport instead of faking a working QR code for it.</summary>
    public bool HasActiveShare { get; set; }

    public IReadOnlyList<Opportunity> RecommendedOpportunities { get; set; } = [];

    public IReadOnlyDictionary<Guid, string> OrganisationNamesByOpportunityId { get; set; } = new Dictionary<Guid, string>();

    public IReadOnlyList<Interview> RecentInterviews { get; set; } = [];
}
