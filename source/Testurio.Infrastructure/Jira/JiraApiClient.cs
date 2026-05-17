using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Jira;

public partial class JiraApiClient : IJiraApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraApiClient> _logger;

    public JiraApiClient(HttpClient httpClient, ILogger<JiraApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Posts <paramref name="commentBody"/> as a plain-text comment on the specified Jira issue.
    /// Uses REST API v2 which accepts wiki markup directly as a string, ensuring that
    /// the Jira-formatted text produced by <c>ReportBuilderService</c> renders correctly.
    /// Returns a <see cref="JiraCommentResult"/> carrying the comment ID on success, or the
    /// HTTP status and error body on failure so callers can record diagnostic detail (AC-014).
    /// </summary>
    public async Task<JiraCommentResult> PostCommentAsync(
        string baseUrl,
        string issueKey,
        string email,
        string apiToken,
        string commentBody,
        CancellationToken cancellationToken = default)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/rest/api/2/issue/{issueKey}/comment");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // API v2 accepts a plain "body" string that supports Jira wiki markup.
        var payload = new { body = commentBody };
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, issueKey, ex);
            return JiraCommentResult.Failure(0, ex.Message);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogCommentFailed(_logger, issueKey, (int)response.StatusCode, errorBody);
            return JiraCommentResult.Failure((int)response.StatusCode, errorBody);
        }

        // Parse the comment ID from the response body (AC-015: PmCommentId must be set).
        string? commentId = null;
        try
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("id", out var idElement))
                commentId = idElement.GetString();
        }
        catch
        {
            // Comment ID is best-effort — the post succeeded so we return success regardless.
        }

        return JiraCommentResult.Success(commentId);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to post comment on Jira issue {IssueKey}: HTTP {StatusCode} — {ErrorBody}")]
    private static partial void LogCommentFailed(
        ILogger logger, string issueKey, int statusCode, string errorBody);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Network error posting comment on Jira issue {IssueKey}")]
    private static partial void LogNetworkError(ILogger logger, string issueKey, Exception ex);
}
