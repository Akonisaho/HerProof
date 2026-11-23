using HerProof.Application.Speech;

namespace HerProof.Infrastructure.Speech;

/// <summary>Used whenever no Speech__Key is configured. Real documented fallback (docs/01 section 15:
/// "Speech STT down -> switch to typed answers, same rubric"), not a placeholder — the text-mode path
/// built in P10 already carries the full rubric with no voice at all, so this exists only so code that
/// unconditionally depends on ISpeechToText/ITextToSpeech doesn't need special-casing.</summary>
public class FakeSpeechToText : ISpeechToText
{
    public Task<TranscriptionResult> TranscribeAsync(byte[] audioBytes, IReadOnlyList<string> phraseHints, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TranscriptionResult(string.Empty));
}

public class FakeTextToSpeech : ITextToSpeech
{
    public Task<SynthesizedSpeech> SynthesizeAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SynthesizedSpeech([], "audio/none"));
}
