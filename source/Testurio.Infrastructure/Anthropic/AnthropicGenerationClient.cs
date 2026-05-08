using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Anthropic;

public class AnthropicGenerationClient : ILlmGenerationClient
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly ILogger<AnthropicGenerationClient> _logger;

    public AnthropicGenerationClient(HttpClient httpClient, string modelId, ILogger<AnthropicGenerationClient> logger)
    {
        _httpClient = httpClient;
        _modelId = modelId;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _modelId,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Anthropic API error {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return text.Trim();
    }
}
