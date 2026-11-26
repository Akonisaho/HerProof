using HerProof.Domain.Common;

namespace HerProof.Domain.Identity;

public enum UserRole
{
    Candidate,
    Recruiter,
    Assessor,
    Partner,
    Administrator,
}

/// <summary>Projection of the authenticated user needed by domain/application logic. The actual
/// authentication identity (ApplicationUser : IdentityUser) lives in Infrastructure (P03) and this
/// entity's Id is kept equal to it.</summary>
public class CandidateProfile : Entity
{
    public Guid UserId { get; init; }
    public string? Province { get; private set; }
    public string? EducationLevel { get; private set; }

    /// <summary>Optional, bursary-eligibility only, never used for AI scoring (docs/02 ERD 1).</summary>
    public string? GenderDeclared { get; private set; }

    /// <summary>Optional, bursary-eligibility only, never used for AI scoring (docs/02 ERD 1).</summary>
    public string? HouseholdIncomeBand { get; private set; }

    public string? CareerInterests { get; private set; }
    public string? GitHubProfile { get; private set; }
    public string? LinkedInProfile { get; private set; }

    /// <summary>Supports the FieldOfStudy eligibility rule type (docs/05 P04). Not in docs/02's ERD 1
    /// table but required by the eligibility rule set the build plan specifies.</summary>
    public string? FieldOfStudy { get; private set; }

    /// <summary>Supports the AgeRange eligibility rule type (docs/05 P04). Optional like the other
    /// bursary-eligibility-only fields; never used for AI scoring.</summary>
    public DateOnly? DateOfBirth { get; private set; }

    private CandidateProfile()
    {
    }

    public CandidateProfile(Guid userId)
    {
        UserId = userId;
    }

    /// <summary>Gates browsing/applying (docs/05): the three fields that matter for every candidate
    /// regardless of opportunity type. Deliberately excludes the bursary-only optional fields
    /// (GenderDeclared, HouseholdIncomeBand, FieldOfStudy, DateOfBirth) and the social links, which
    /// stay genuinely optional.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Province)
        && !string.IsNullOrWhiteSpace(EducationLevel)
        && !string.IsNullOrWhiteSpace(CareerInterests);

    public void UpdateProfile(
        string? province,
        string? educationLevel,
        string? genderDeclared,
        string? householdIncomeBand,
        string? careerInterests,
        string? gitHubProfile,
        string? linkedInProfile,
        string? fieldOfStudy = null,
        DateOnly? dateOfBirth = null)
    {
        Province = province;
        EducationLevel = educationLevel;
        GenderDeclared = genderDeclared;
        HouseholdIncomeBand = householdIncomeBand;
        CareerInterests = careerInterests;
        FieldOfStudy = fieldOfStudy;
        DateOfBirth = dateOfBirth;
        GitHubProfile = gitHubProfile;
        LinkedInProfile = linkedInProfile;
    }
}

public enum OrganisationVerificationStatus
{
    Unverified,
    Verified,
}

public class Organisation : Entity
{
    public string Name { get; private set; }
    public string? Industry { get; private set; }
    public string? Website { get; private set; }
    public OrganisationVerificationStatus VerificationStatus { get; private set; } = OrganisationVerificationStatus.Unverified;

    private Organisation()
    {
        Name = string.Empty;
    }

    public Organisation(string name, string? industry, string? website)
    {
        Name = name;
        Industry = industry;
        Website = website;
    }

    public void MarkVerified() => VerificationStatus = OrganisationVerificationStatus.Verified;
}

public enum OrganisationRole
{
    Recruiter,
    Partner,
    Administrator,
}

public class OrganisationMember : Entity
{
    public Guid OrganisationId { get; init; }
    public Guid UserId { get; init; }
    public OrganisationRole OrganisationRole { get; init; }

    private OrganisationMember()
    {
    }

    public OrganisationMember(Guid organisationId, Guid userId, OrganisationRole organisationRole)
    {
        OrganisationId = organisationId;
        UserId = userId;
        OrganisationRole = organisationRole;
    }
}
