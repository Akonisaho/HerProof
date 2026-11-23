using HerProof.Application.Interviews;
using Xunit;

namespace HerProof.Application.Tests;

public class PhraseHintExtractorTests
{
    [Fact]
    public void Extracts_identifier_shaped_tokens()
    {
        var hints = PhraseHintExtractor.Extract("public async Task<TaskItem> CreateAsync(string title)");

        Assert.Contains("public", hints);
        Assert.Contains("async", hints);
        Assert.Contains("TaskItem", hints);
        Assert.Contains("CreateAsync", hints);
    }

    [Fact]
    public void Deduplicates_tokens_across_sources()
    {
        var hints = PhraseHintExtractor.Extract("TaskService.cs", "class TaskService : ITaskService");

        Assert.Single(hints, h => h == "TaskService");
    }

    [Fact]
    public void Ignores_null_or_blank_sources()
    {
        var hints = PhraseHintExtractor.Extract(null, "", "  ", "RealToken");

        Assert.Equal(["RealToken"], hints);
    }

    [Fact]
    public void Caps_the_number_of_hints()
    {
        var manyWords = string.Join(" ", Enumerable.Range(0, 50).Select(i => $"identifierToken{i}"));

        var hints = PhraseHintExtractor.Extract(manyWords);

        Assert.True(hints.Count <= 20);
    }
}
