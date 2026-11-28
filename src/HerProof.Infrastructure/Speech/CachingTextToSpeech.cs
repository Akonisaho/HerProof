using System.Security.Cryptography;
using System.Text;
using HerProof.Application.Speech;
using Microsoft.Extensions.Caching.Memory;

namespace HerProof.Infrastructure.Speech;

/// <summary>Caches synthesized audio by a hash of the text (docs/06 section 9 point 1: "cache by text
/// hash"). Nolwazi asks a fixed catalogue of scripted phrases ("Can you say more?", "Let's move on")
/// across many interviews, so this meaningfully cuts both latency and provider cost.</summary>
public class CachingTextToSpeech(ITextToSpeech inner, IMemoryCache cache) : ITextToSpeech
{
    public async Task<SynthesizedSpeech> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var key = $"tts:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}";
        if (cache.TryGetValue(key, out SynthesizedSpeech? cached) && cached is not null)
        {
            return cached;
        }

        var result = await inner.SynthesizeAsync(text, cancellationToken);
        cache.Set(key, result, TimeSpan.FromDays(7));
        return result;
    }
}
