namespace HerProof.Application.Speech;

public record TranscriptionResult(string Text);

/// <summary>ADR-05: server-side STT/TTS, never in the browser (consistent quality, vocabulary hints,
/// key never exposed to the client). Phrase hints come from the repo's own identifiers (docs/06
/// section 9) so Thandi's questions about the candidate's own code transcribe accurately.</summary>
public interface ISpeechToText
{
    Task<TranscriptionResult> TranscribeAsync(byte[] audioBytes, IReadOnlyList<string> phraseHints, CancellationToken cancellationToken = default);
}

public record SynthesizedSpeech(byte[] AudioBytes, string ContentType);

public interface ITextToSpeech
{
    Task<SynthesizedSpeech> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
}
