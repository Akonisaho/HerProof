using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HerProof.Application.AI;

namespace HerProof.AI;

/// <summary>Real LLM path, used only when AI__ApiKey is configured (Infrastructure wiring decides
/// which implementation to register — see docs/STUBS.md). Requests JSON-only output; this class does
/// not itself validate the JSON shape — HerProof.Application.Repositories.FindingVerification and the
/// caller's deserialization step do that (docs/03 section 6 rule 2).</summary>
public class AnthropicAiService(HttpClient httpClient, string model) : IAIService
{
    public async Task<AiCompletion> CompleteJsonAsync(string systemPrompt, string untrustedUserContent, string promptVersion, CancellationToken cancellationToken = default)
    {
        var request = new AnthropicRequest(
            model,
            4096,
            systemPrompt,
            [new AnthropicMessage("user", untrustedUserContent)]);

        using var response = await httpClient.PostAsJsonAsync("v1/messages", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Anthropic API returned an empty response.");

        var text = payload.Content.FirstOrDefault(c => c.Type == "text")?.Text
            ?? throw new InvalidOperationException("Anthropic API response contained no text content.");

        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt + untrustedUserContent)));
        return new AiCompletion(text, payload.Model, promptVersion, inputHash);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private record AnthropicRequest(string Model, [property: JsonPropertyName("max_tokens")] int MaxTokens, string System, AnthropicMessage[] Messages);

    private record AnthropicMessage(string Role, string Content);

    private record AnthropicResponse(string Model, List<AnthropicContentBlock> Content);

    private record AnthropicContentBlock(string Type, string? Text);
}
