using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Clients;

public partial class JiraApiClient : IJiraApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraApiClient> _logger;

    public JiraApiClient(HttpClient httpClient, ILogger<JiraApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> PostCommentAsync(string baseUrl, string issueKey, string email, string apiToken, string commentBody, CancellationToken cancellationToken = default)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/rest/api/3/issue/{issueKey}/comment");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[] { new { type = "text", text = commentBody } }
                    }
                }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogCommentFailed(_logger, issueKey, (int)response.StatusCode);
            return false;
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to post comment on Jira issue {IssueKey}: HTTP {StatusCode}")]
    private static partial void LogCommentFailed(ILogger logger, string issueKey, int statusCode);
}
