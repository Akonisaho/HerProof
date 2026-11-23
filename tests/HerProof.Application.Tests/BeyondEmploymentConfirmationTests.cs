using HerProof.Application.Experience;
using HerProof.Domain.Competencies;
using Xunit;

namespace HerProof.Application.Tests;

public class BeyondEmploymentConfirmationTests
{
    [Fact]
    public void Confirming_with_no_existing_record_creates_a_claimed_competency()
    {
        var candidateId = Guid.NewGuid();
        var competencyId = Guid.NewGuid();

        var result = BeyondEmploymentConfirmation.EnsureClaimed(candidateId, competencyId, declaredLevel: 2, existing: null, DateTime.UtcNow);

        Assert.Equal(CandidateCompetencyStatus.Claimed, result.Status);
        Assert.Equal(candidateId, result.CandidateId);
        Assert.Equal(competencyId, result.CompetencyId);
        Assert.Null(result.VerifiedLevel);
    }

    [Fact]
    public void Confirming_never_downgrades_an_already_verified_competency()
    {
        var existing = new CandidateCompetency(Guid.NewGuid(), Guid.NewGuid(), declaredLevel: 2, DateTime.UtcNow);
        existing.ApplyAssessorDecision(CandidateCompetencyStatus.Verified, finalLevel: 3, DateTime.UtcNow);

        var result = BeyondEmploymentConfirmation.EnsureClaimed(existing.CandidateId, existing.CompetencyId, 2, existing, DateTime.UtcNow);

        Assert.Same(existing, result);
        Assert.Equal(CandidateCompetencyStatus.Verified, result.Status);
        Assert.Equal(3, result.VerifiedLevel);
    }
}
