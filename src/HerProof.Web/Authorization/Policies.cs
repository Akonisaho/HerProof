using HerProof.Domain.Identity;

namespace HerProof.Web.Authorization;

/// <summary>One role-gate policy per role (CLAUDE.md roles). Resource ownership on top of these is
/// IResourceAuthorizer's job, not the policy's.</summary>
public static class Policies
{
    public const string RequireCandidate = nameof(RequireCandidate);
    public const string RequireRecruiter = nameof(RequireRecruiter);
    public const string RequireAssessor = nameof(RequireAssessor);
    public const string RequirePartner = nameof(RequirePartner);
    public const string RequireAdministrator = nameof(RequireAdministrator);

    public static void AddHerProofPolicies(this Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(RequireCandidate, p => p.RequireRole(nameof(UserRole.Candidate)));
        options.AddPolicy(RequireRecruiter, p => p.RequireRole(nameof(UserRole.Recruiter)));
        options.AddPolicy(RequireAssessor, p => p.RequireRole(nameof(UserRole.Assessor)));
        options.AddPolicy(RequirePartner, p => p.RequireRole(nameof(UserRole.Partner)));
        options.AddPolicy(RequireAdministrator, p => p.RequireRole(nameof(UserRole.Administrator)));
    }
}
