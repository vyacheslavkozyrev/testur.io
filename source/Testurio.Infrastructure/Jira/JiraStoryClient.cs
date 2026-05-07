using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Jira;

public partial class JiraStoryClient : IJiraStoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraStoryClient> _logger;

    public JiraStoryClient(HttpClient httpClient, ILogger<JiraStoryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<JiraStoryContent?> GetStoryContentAsync(
        string baseUrl,
        string issueKey,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/rest/api/3/issue/{issueKey}?fields=description");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogFetchFailed(_logger, issueKey, (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        JiraIssueResponse? issue;
        try
        {
            issue = JsonSerializer.Deserialize<JiraIssueResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, issueKey, ex);
            return null;
        }

        var description = ExtractAdfText(issue?.Fields?.Description);

        if (string.IsNullOrWhiteSpace(description))
            return null;

        return new JiraStoryContent
        {
            Description = description,
            AcceptanceCriteria = string.Empty
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch Jira story {IssueKey}: HTTP {StatusCode}")]
    private static partial void LogFetchFailed(ILogger logger, string issueKey, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize Jira story response for {IssueKey}")]
    private static partial void LogDeserializationFailed(ILogger logger, string issueKey, Exception ex);

    private sealed class JiraIssueResponse
    {
        [JsonPropertyName("fields")]
        public JiraFieldsResponse? Fields { get; init; }
    }

    private sealed class JiraFieldsResponse
    {
        [JsonPropertyName("description")]
        public JsonElement? Description { get; init; }
    }

    // Jira API v3 returns description/AC as Atlassian Document Format (ADF) — a nested JSON object.
    // This walks the ADF tree and extracts all plain text content.
    private static string ExtractAdfText(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null)
            return string.Empty;

        var sb = new StringBuilder();
        ExtractText(element.Value, sb);
        return sb.ToString().Trim();
    }

    private static void ExtractText(JsonElement node, StringBuilder sb)
    {
        if (node.ValueKind == JsonValueKind.String)
        {
            sb.Append(node.GetString());
            return;
        }

        if (node.ValueKind != JsonValueKind.Object)
            return;

        if (node.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            sb.Append(text.GetString());

        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in content.EnumerateArray())
            {
                ExtractText(child, sb);
                if (child.TryGetProperty("type", out var type) && type.GetString() is "paragraph" or "heading")
                    sb.Append('\n');
            }
        }
    }
}
