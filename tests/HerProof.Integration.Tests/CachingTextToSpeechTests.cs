using HerProof.Application.Speech;
using HerProof.Infrastructure.Speech;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace HerProof.Integration.Tests;

public class CachingTextToSpeechTests
{
    private sealed class CountingTextToSpeech : ITextToSpeech
    {
        public int CallCount { get; private set; }

        public Task<SynthesizedSpeech> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SynthesizedSpeech([1, 2, 3], "audio/mpeg"));
        }
    }

    [Fact]
    public async Task Same_text_is_synthesized_only_once()
    {
        var inner = new CountingTextToSpeech();
        var caching = new CachingTextToSpeech(inner, new MemoryCache(new MemoryCacheOptions()));

        await caching.SynthesizeAsync("Can you walk me through this?");
        await caching.SynthesizeAsync("Can you walk me through this?");

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task Different_text_is_synthesized_separately()
    {
        var inner = new CountingTextToSpeech();
        var caching = new CachingTextToSpeech(inner, new MemoryCache(new MemoryCacheOptions()));

        await caching.SynthesizeAsync("First utterance.");
        await caching.SynthesizeAsync("Second utterance.");

        Assert.Equal(2, inner.CallCount);
    }
}
