using HerProof.Application.Opportunities;
using Xunit;

namespace HerProof.Application.Tests;

public class AppliedCredentialSelectorTests
{
    [Fact]
    public void Keeps_only_identifiers_the_candidate_actually_owns()
    {
        var result = AppliedCredentialSelector.Select(
            ownedCredentialIdentifiers: ["HP-OWNED-1", "HP-OWNED-2"],
            requestedIdentifiers: ["HP-OWNED-1", "HP-NOT-OWNED"]);

        Assert.Equal(["HP-OWNED-1"], result);
    }

    [Fact]
    public void Returns_empty_when_nothing_requested_is_owned()
    {
        var result = AppliedCredentialSelector.Select(["HP-OWNED-1"], ["HP-OTHER"]);

        Assert.Empty(result);
    }

    [Fact]
    public void Deduplicates_requested_identifiers()
    {
        var result = AppliedCredentialSelector.Select(["HP-OWNED-1"], ["HP-OWNED-1", "HP-OWNED-1"]);

        Assert.Single(result);
    }
}
