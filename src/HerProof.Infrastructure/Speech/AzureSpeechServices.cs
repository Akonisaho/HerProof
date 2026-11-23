using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using HerProof.Application.Speech;
using Microsoft.Extensions.Configuration;

namespace HerProof.Infrastructure.Speech;

/// <summary>Real Azure Cognitive Services Speech REST implementation (ADR-05: en-ZA, server-side).
/// STUB(HP-023): written against Azure's documented REST contracts but not exercised against a live
/// key in this environment — no Speech__Key is available here. Review against a real key before
/// relying on it for a rehearsal.</summary>
public class AzureSpeechToText(HttpClient httpClient, IConfiguration configuration) : ISpeechToText
{
    public async Task<TranscriptionResult> TranscribeAsync(byte[] audioBytes, IReadOnlyList<string> phraseHints, CancellationToken cancellationToken = default)
    {
        var locale = configuration["Speech:Locale"] ?? "en-ZA";
        var query = $"language={Uri.EscapeDataString(locale)}&format=detailed";
        if (phraseHints.Count > 0)
        {
            // Azure's short-audio REST endpoint doesn't support phrase lists directly; the SDK-based
            // real-time endpoint does. Documented limitation, not silently ignored.
        }

        using var content = new ByteArrayContent(audioBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        using var response = await httpClient.PostAsync($"speech/recognition/conversation/cognitiveservices/v1?{query}", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = System.Text.Json.JsonDocument.Parse(json).RootElement.TryGetProperty("DisplayText", out var displayText)
            ? displayText.GetString() ?? string.Empty
            : string.Empty;

        return new TranscriptionResult(text);
    }
}

public class AzureTextToSpeech(HttpClient httpClient, IConfiguration configuration) : ITextToSpeech
{
    public async Task<SynthesizedSpeech> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var locale = configuration["Speech:Locale"] ?? "en-ZA";
        var voiceName = locale == "en-ZA" ? "en-ZA-LeahNeural" : "en-US-JennyNeural";

        var ssml = new XElement("speak",
            new XAttribute("version", "1.0"),
            new XAttribute(XNamespace.Xml + "lang", locale),
            new XElement("voice", new XAttribute("name", voiceName), text)).ToString(SaveOptions.DisableFormatting);

        using var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        using var request = new HttpRequestMessage(HttpMethod.Post, "cognitiveservices/v1") { Content = content };
        request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-64kbitrate-mono-mp3");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new SynthesizedSpeech(audioBytes, "audio/mpeg");
    }
}
