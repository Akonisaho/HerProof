using HerProof.Domain.Identity;
using HerProof.Domain.Partnerships;

namespace HerProof.Web.Models.Admin;

public class AdminUserRow
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}

public class AssignRolesViewModel
{
    public bool Recruiter { get; set; }
    public bool Assessor { get; set; }
    public bool Partner { get; set; }
    public bool Administrator { get; set; }
}

public class OrganisationRow
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required int MemberCount { get; init; }
}

public class OrganisationDetailViewModel
{
    public required Organisation Organisation { get; init; }
    public required IReadOnlyList<(Guid MemberId, string Email, OrganisationRole Role)> Members { get; init; }
}

public class CreateOrganisationViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Website { get; set; }
}

public class AddOrganisationMemberViewModel
{
    public string Email { get; set; } = string.Empty;
    public OrganisationRole Role { get; set; }
}

public class PartnerInquiryRow
{
    public required Guid Id { get; init; }
    public required string OrganisationName { get; init; }
    public required string ContactName { get; init; }
    public required string ContactEmail { get; init; }
    public string? Phone { get; init; }
    public required PartnershipInquiryType InquiryType { get; init; }
    public required string Message { get; init; }
    public required PartnershipInquiryStatus Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
