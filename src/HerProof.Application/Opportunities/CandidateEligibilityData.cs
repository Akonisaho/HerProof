using HerProof.Domain.Identity;

namespace HerProof.Application.Opportunities;

/// <summary>What eligibility rules are evaluated against. Null fields mean the candidate has not
/// provided that data — never treated as a failure (docs/02: "Unknown arises when the candidate has
/// not provided the data").</summary>
public record CandidateEligibilityData(
    string? Province,
    string? GenderDeclared,
    string? EducationLevel,
    string? HouseholdIncomeBand,
    string? FieldOfStudy,
    DateOnly? DateOfBirth)
{
    public static CandidateEligibilityData FromProfile(CandidateProfile profile) => new(
        profile.Province,
        profile.GenderDeclared,
        profile.EducationLevel,
        profile.HouseholdIncomeBand,
        profile.FieldOfStudy,
        profile.DateOfBirth);
}
