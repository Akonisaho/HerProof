using HerProof.Application.Passports;
using HerProof.Domain.Passports;
using HerProof.Domain.Verification;
using Xunit;

namespace HerProof.Application.Tests;

public class PassportIssuanceServiceTests
{
    private static VerificationRecord VerifiedRecord(int finalLevel = 2) => new(
        candidateId: Guid.NewGuid(), competencyId: Guid.NewGuid(), assessmentId: Guid.NewGuid(),
        finalLevel: finalLevel, assessorId: Guid.NewGuid(), decision: VerificationDecision.Verified,
        evidenceSummary: "Looks solid.", createdAtUtc: DateTime.UtcNow, supersedesId: null);

    [Fact]
    public void EnsurePassport_creates_one_when_none_exists()
    {
        var passport = PassportIssuanceService.EnsurePassport(null, Guid.NewGuid(), DateTime.UtcNow);

        Assert.StartsWith("HP-", passport.PassportNumber);
    }

    [Fact]
    public void EnsurePassport_reuses_the_existing_one()
    {
        var existing = new SkillsPassport(Guid.NewGuid(), "HP-2026-ABCDEF12", DateTime.UtcNow);

        var result = PassportIssuanceService.EnsurePassport(existing, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Same(existing, result);
    }

    [Fact]
    public void IssueForVerification_refuses_a_non_verified_decision()
    {
        var passport = new SkillsPassport(Guid.NewGuid(), "HP-2026-ABCDEF12", DateTime.UtcNow);
        var record = new VerificationRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), VerificationDecision.Developing, "Not yet.", DateTime.UtcNow, null);

        var credential = PassportIssuanceService.IssueForVerification(passport, record, "FINMGMT", DateTime.UtcNow);

        Assert.Null(credential);
        Assert.Empty(passport.Credentials);
    }

    [Fact]
    public void IssueForVerification_issues_exactly_once_for_the_same_record()
    {
        var passport = new SkillsPassport(Guid.NewGuid(), "HP-2026-ABCDEF12", DateTime.UtcNow);
        var record = VerifiedRecord();

        var first = PassportIssuanceService.IssueForVerification(passport, record, "FINMGMT", DateTime.UtcNow);
        var second = PassportIssuanceService.IssueForVerification(passport, record, "FINMGMT", DateTime.UtcNow);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Single(passport.Credentials);
    }

    [Fact]
    public void IssueForVerification_sets_a_validity_window()
    {
        var passport = new SkillsPassport(Guid.NewGuid(), "HP-2026-ABCDEF12", DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var record = VerifiedRecord();

        var credential = PassportIssuanceService.IssueForVerification(passport, record, "FINMGMT", now);

        Assert.NotNull(credential);
        Assert.Equal(now.Add(PassportIssuanceService.DefaultValidity), credential!.ValidUntilUtc);
    }
}
