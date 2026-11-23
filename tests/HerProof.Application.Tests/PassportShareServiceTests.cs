using HerProof.Application.Passports;
using HerProof.Domain.Passports;
using Xunit;

namespace HerProof.Application.Tests;

public class PassportShareServiceTests
{
    private static SkillsPassport PassportWithOneCredential(out string credentialId)
    {
        var passport = new SkillsPassport(Guid.NewGuid(), "HP-2026-ABCDEF12", DateTime.UtcNow);
        var credential = passport.IssueCredential(Guid.NewGuid(), "HP-2026-ABCDEF12-FINMGMT-123456", DateTime.UtcNow, null);
        credentialId = credential.CredentialIdentifier;
        return passport;
    }

    [Fact]
    public void CreateShare_rejects_an_identifier_that_does_not_belong_to_this_passport()
    {
        var passport = PassportWithOneCredential(out _);

        Assert.Throws<ArgumentException>(() =>
            PassportShareService.CreateShare(passport, ["not-a-real-credential"], DateTime.UtcNow));
    }

    [Fact]
    public void CreateShare_only_keeps_identifiers_that_belong_to_the_passport()
    {
        var passport = PassportWithOneCredential(out var realId);

        var (share, rawToken) = PassportShareService.CreateShare(passport, [realId, "bogus-id"], DateTime.UtcNow);

        Assert.Equal([realId], share.VisibleFields);
        Assert.NotEmpty(rawToken);
        Assert.NotEqual(rawToken, share.TokenHash);
    }

    [Fact]
    public void Resolve_finds_the_share_matching_the_raw_token()
    {
        var passport = PassportWithOneCredential(out var realId);
        var (share, rawToken) = PassportShareService.CreateShare(passport, [realId], DateTime.UtcNow);

        var resolved = PassportShareService.Resolve(passport.Shares, rawToken, DateTime.UtcNow);

        Assert.Same(share, resolved);
    }

    [Fact]
    public void Resolve_rejects_a_wrong_token()
    {
        var passport = PassportWithOneCredential(out var realId);
        PassportShareService.CreateShare(passport, [realId], DateTime.UtcNow);

        var resolved = PassportShareService.Resolve(passport.Shares, "not-the-real-token", DateTime.UtcNow);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_rejects_a_revoked_share()
    {
        var passport = PassportWithOneCredential(out var realId);
        var (share, rawToken) = PassportShareService.CreateShare(passport, [realId], DateTime.UtcNow);
        share.Revoke(DateTime.UtcNow);

        var resolved = PassportShareService.Resolve(passport.Shares, rawToken, DateTime.UtcNow);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_rejects_an_expired_share()
    {
        var passport = PassportWithOneCredential(out var realId);
        var now = DateTime.UtcNow;
        var (_, rawToken) = PassportShareService.CreateShare(passport, [realId], now, now.AddDays(1));

        var resolved = PassportShareService.Resolve(passport.Shares, rawToken, now.AddDays(2));

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_accepts_a_share_before_its_expiry()
    {
        var passport = PassportWithOneCredential(out var realId);
        var now = DateTime.UtcNow;
        var (share, rawToken) = PassportShareService.CreateShare(passport, [realId], now, now.AddDays(1));

        var resolved = PassportShareService.Resolve(passport.Shares, rawToken, now.AddHours(1));

        Assert.Same(share, resolved);
    }
}
