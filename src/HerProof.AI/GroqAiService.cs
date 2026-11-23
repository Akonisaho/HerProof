using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HerProof.Application.AI;

namespace HerProof.AI;

/// <summary>Real LLM path over Groq's free, OpenAI-compatible chat completions API (used only when
/// Groq:ApiKey is configured — see Program.cs for which implementation gets registered). Same
/// JSON-only contract as AnthropicAiService: this class returns text, nothing else — there is no
/// tool-use surface here to misuse (docs/03 section 6 rule 4).</summary>
public class GroqAiService(HttpClient httpClient, string model) : IAIService
{
    public async Task<AiCompletion> CompleteJsonAsync(string systemPrompt, string untrustedUserContent, string promptVersion, CancellationToken cancellationToken = default)
    {
        var request = new GroqRequest(
            model,
            [new GroqMessage("system", systemPrompt), new GroqMessage("user", untrustedUserContent)],
            new GroqResponseFormat("json_object"));

        using var response = await httpClient.PostAsJsonAsync("openai/v1/chat/completions", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GroqResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Groq API returned an empty response.");

        var text = payload.Choices.FirstOrDefault()?.Message.Content
            ?? throw new InvalidOperationException("Groq API response contained no message content.");

        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt + untrustedUserContent)));
        return new AiCompletion(text, payload.Model, promptVersion, inputHash);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private record GroqRequest(string Model, GroqMessage[] Messages, [property: JsonPropertyName("response_format")] GroqResponseFormat ResponseFormat);

    private record GroqMessage(string Role, string Content);

    private record GroqResponseFormat(string Type);

    private record GroqResponse(string Model, List<GroqChoice> Choices);

    private record GroqChoice(GroqMessage Message);
}
