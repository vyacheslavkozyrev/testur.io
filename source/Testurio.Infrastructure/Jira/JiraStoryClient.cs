using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Jira;

public partial class JiraStoryClient : IJiraStoryClient
{
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
            $"{baseUrl.TrimEnd('/')}/rest/api/3/issue/{issueKey}?fields=description,customfield_10016");
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
            issue = JsonSerializer.Deserialize<JiraIssueResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, issueKey, ex);
            return null;
        }

        var description = issue?.Fields?.Description;
        var ac = issue?.Fields?.CustomField10016;

        if (string.IsNullOrWhiteSpace(description))
            return null;

        return new JiraStoryContent
        {
            Description = description,
            AcceptanceCriteria = string.IsNullOrWhiteSpace(ac) ? string.Empty : ac
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
        public string? Description { get; init; }

        // Jira stores acceptance criteria as a custom field — key matches the Jira instance configuration.
        [JsonPropertyName("customfield_10016")]
        public string? CustomField10016 { get; init; }
    }
}
