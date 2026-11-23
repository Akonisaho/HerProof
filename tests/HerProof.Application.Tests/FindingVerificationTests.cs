using HerProof.Application.Repositories;
using Xunit;

namespace HerProof.Application.Tests;

public class FindingVerificationTests
{
    private static readonly FetchedRepositoryContent Content = new("abc123", [
        new FetchedRepositoryFile("src/OrderService.cs", string.Join('\n', Enumerable.Range(1, 50).Select(i => $"// line {i}"))),
    ]);

    [Fact]
    public void Finding_referencing_a_real_file_and_line_is_verified()
    {
        var finding = new LlmFinding("C#", "class", "src/OrderService.cs", "10-20", "desc", "evidence", "High");

        var result = FindingVerification.VerifyAndFilter([finding], Content);

        Assert.Single(result);
    }

    [Fact]
    public void Finding_referencing_a_file_not_in_the_fetched_set_is_discarded()
    {
        // This is the actual mechanical defence against prompt injection: no matter what the LLM
        // claims, if it points at a file that was never fetched at the pinned commit, it's discarded.
        var finding = new LlmFinding("C#", "class", "src/DoesNotExist.cs", "1", "desc", "evidence", "High");

        var result = FindingVerification.VerifyAndFilter([finding], Content);

        Assert.Empty(result);
    }

    [Fact]
    public void Finding_with_a_line_range_beyond_the_files_length_is_discarded()
    {
        var finding = new LlmFinding("C#", "class", "src/OrderService.cs", "45-200", "desc", "evidence", "High");

        var result = FindingVerification.VerifyAndFilter([finding], Content);

        Assert.Empty(result);
    }

    [Fact]
    public void Finding_with_a_single_line_number_is_verified()
    {
        var finding = new LlmFinding("C#", "class", "src/OrderService.cs", "5", "desc", "evidence", "High");

        var result = FindingVerification.VerifyAndFilter([finding], Content);

        Assert.Single(result);
    }

    [Fact]
    public void Finding_with_no_line_reference_is_verified_if_the_file_exists()
    {
        var finding = new LlmFinding("C#", "manifest", "src/OrderService.cs", null, "desc", "evidence", "High");

        var result = FindingVerification.VerifyAndFilter([finding], Content);

        Assert.Single(result);
    }

    [Fact]
    public void Finding_with_an_unparseable_line_reference_is_discarded()
    {
        var finding = new LlmFinding("C#", "class", "src/OrderService.cs", "not-a-line", "desc", "evidence", "High");

        var result = FindingVerification.VerifyAndFilter([finding], Content);

        Assert.Empty(result);
    }
}
