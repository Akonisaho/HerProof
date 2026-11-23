using HerProof.Application.Verification;
using HerProof.Domain.Verification;
using Xunit;

namespace HerProof.Application.Tests;

public class VerificationDecisionServiceTests
{
    private static void CreateWith(bool isAssessor, VerificationDecision decision = VerificationDecision.Verified, int? finalLevel = 2) =>
        VerificationDecisionService.Create(isAssessor, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), finalLevel, Guid.NewGuid(), decision, "evidence", DateTime.UtcNow, null);

    [Theory]
    [InlineData(VerificationDecision.Verified)]
    [InlineData(VerificationDecision.Developing)]
    [InlineData(VerificationDecision.NotDemonstrated)]
    public void Every_non_assessor_path_to_every_decision_is_rejected(VerificationDecision decision)
    {
        // The exact rule docs/05 P14 asks for: "every non-assessor path to Verified is rejected" —
        // checked here for all three decisions, not just Verified, since CLAUDE.md rule 1 says no
        // code path may set ANY of these without an Assessor.
        Assert.Throws<UnauthorizedAccessException>(() => CreateWith(isAssessor: false, decision, finalLevel: decision == VerificationDecision.Verified ? 2 : null));
    }

    [Fact]
    public void Assessor_can_create_a_verified_decision_with_a_final_level()
    {
        var record = VerificationDecisionService.Create(
            true, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, Guid.NewGuid(),
            VerificationDecision.Verified, "evidence", DateTime.UtcNow, null);

        Assert.Equal(VerificationDecision.Verified, record.Decision);
        Assert.Equal(3, record.FinalLevel);
    }

    [Fact]
    public void Assessor_can_create_a_developing_decision_with_no_final_level()
    {
        var record = VerificationDecisionService.Create(
            true, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            VerificationDecision.Developing, "evidence", DateTime.UtcNow, null);

        Assert.Equal(VerificationDecision.Developing, record.Decision);
        Assert.Null(record.FinalLevel);
    }

    [Fact]
    public void A_new_decision_can_supersede_a_previous_one()
    {
        var previousId = Guid.NewGuid();

        var record = VerificationDecisionService.Create(
            true, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, Guid.NewGuid(),
            VerificationDecision.Verified, "reassessed", DateTime.UtcNow, previousId);

        Assert.Equal(previousId, record.SupersedesId);
    }
}
