using System.Security.Claims;

namespace HerProof.Application.Authorization;

/// <summary>Resource ownership checks beyond role policies (CLAUDE.md working agreement, docs/05 P03):
/// a candidate may read only her own records, a recruiter only her own organisation's, an assessor
/// only assignments given to her. Role policies answer "can a Candidate do this kind of thing";
/// this answers "can THIS user touch THIS specific resource".</summary>
public interface IResourceAuthorizer
{
    Task<bool> CanAccessCandidateProfileAsync(ClaimsPrincipal user, Guid candidateProfileId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessOrganisationAsync(ClaimsPrincipal user, Guid organisationId, CancellationToken cancellationToken = default);
}
