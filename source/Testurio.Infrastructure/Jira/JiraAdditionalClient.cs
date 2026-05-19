using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure.Jira.Models;

namespace Testurio.Infrastructure.Jira;

/// <summary>
/// Extends Jira integration with project-level and webhook management operations
/// required by the PM tool integration feature (0007).
/// Comment posting is handled by the existing <see cref="JiraApiClient"/>.
/// </summary>
public partial class JiraAdditionalClient : IJiraClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly ILogger<JiraAdditionalClient> _logger;

    public JiraAdditionalClient(
        HttpClient httpClient,
        IJiraApiClient jiraApiClient,
        ILogger<JiraAdditionalClient> logger)
    {
        _httpClient = httpClient;
        _jiraApiClient = jiraApiClient;
        _logger = logger;
    }

    public async Task<JiraProjectInfo?> GetProjectAsync(
        string baseUrl,
        string projectKey,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/project/{Uri.EscapeDataString(projectKey)}";
        var request = BuildBasicAuthRequest(HttpMethod.Get, url, email, apiToken);

        return await FetchProjectAsync(request, projectKey, cancellationToken);
    }

    public async Task<JiraProjectInfo?> GetProjectWithPatAsync(
        string baseUrl,
        string projectKey,
        string pat,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/project/{Uri.EscapeDataString(projectKey)}";
        var request = BuildBearerRequest(HttpMethod.Get, url, pat);

        return await FetchProjectAsync(request, projectKey, cancellationToken);
    }

    public async Task<JiraConnectionTestResult> TestConnectionAsync(
        string baseUrl,
        string projectKey,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/project/{Uri.EscapeDataString(projectKey)}";
        var request = BuildBasicAuthRequest(HttpMethod.Get, url, email, apiToken);

        return await ExecuteTestConnectionAsync(request, baseUrl, cancellationToken);
    }

    public async Task<JiraConnectionTestResult> TestConnectionWithPatAsync(
        string baseUrl,
        string projectKey,
        string pat,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/project/{Uri.EscapeDataString(projectKey)}";
        var request = BuildBearerRequest(HttpMethod.Get, url, pat);

        return await ExecuteTestConnectionAsync(request, baseUrl, cancellationToken);
    }

    public Task<JiraCommentResult> PostCommentAsync(
        string baseUrl,
        string issueKey,
        string email,
        string apiToken,
        string commentBody,
        CancellationToken cancellationToken = default)
        => _jiraApiClient.PostCommentAsync(baseUrl, issueKey, email, apiToken, commentBody, cancellationToken);

    public async Task DeregisterWebhookAsync(
        string baseUrl,
        string webhookId,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/rest/api/3/webhook/{webhookId}";
        var request = BuildBasicAuthRequest(HttpMethod.Delete, url, email, apiToken);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                LogDeregisterFailed(_logger, webhookId, (int)response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            // Deregistration failures are logged but never surfaced (AC-048).
            LogNetworkError(_logger, baseUrl, ex);
        }
    }

    private async Task<JiraProjectInfo?> FetchProjectAsync(
        HttpRequestMessage request,
        string projectKey,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, request.RequestUri?.Host ?? "unknown", ex);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogProjectFetchFailed(_logger, projectKey, (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var project = JsonSerializer.Deserialize<JiraProjectResponse>(body, JsonOptions);

        if (project?.Id is null || project.Key is null || project.Name is null)
            return null;

        return new JiraProjectInfo(project.Id, project.Key, project.Name);
    }

    private async Task<JiraConnectionTestResult> ExecuteTestConnectionAsync(
        HttpRequestMessage request,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, baseUrl, ex);
            return new JiraConnectionTestResult(false, 0, ex.Message);
        }

        if (response.IsSuccessStatusCode)
            return new JiraConnectionTestResult(true, (int)response.StatusCode, null);

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return new JiraConnectionTestResult(false, (int)response.StatusCode, errorBody);
    }

    private static HttpRequestMessage BuildBasicAuthRequest(HttpMethod method, string url, string email, string apiToken)
    {
        var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static HttpRequestMessage BuildBearerRequest(HttpMethod method, string url, string pat)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <inheritdoc />
    public async Task<JiraTransitionResult> TransitionIssueStatusAsync(
        string baseUrl,
        string issueKey,
        string email,
        string apiToken,
        string targetStatusName,
        CancellationToken cancellationToken = default)
    {
        // Step 1: fetch available transitions for the issue.
        var transitionsUrl = $"{baseUrl.TrimEnd('/')}/rest/api/3/issue/{Uri.EscapeDataString(issueKey)}/transitions";
        var getRequest = BuildBasicAuthRequest(HttpMethod.Get, transitionsUrl, email, apiToken);

        HttpResponseMessage getResponse;
        try
        {
            getResponse = await _httpClient.SendAsync(getRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, baseUrl, ex);
            return new JiraTransitionResult(false, 0, ex.Message);
        }

        if (!getResponse.IsSuccessStatusCode)
        {
            var errorBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            LogTransitionFetchFailed(_logger, issueKey, (int)getResponse.StatusCode);
            return new JiraTransitionResult(false, (int)getResponse.StatusCode, errorBody);
        }

        // Step 2: find the transition ID whose name matches targetStatusName (case-insensitive).
        string? transitionId;
        try
        {
            var body = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            transitionId = null;
            if (doc.RootElement.TryGetProperty("transitions", out var transitions))
            {
                foreach (var t in transitions.EnumerateArray())
                {
                    if (t.TryGetProperty("name", out var name) &&
                        string.Equals(name.GetString(), targetStatusName, StringComparison.OrdinalIgnoreCase) &&
                        t.TryGetProperty("id", out var id))
                    {
                        transitionId = id.GetString();
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return new JiraTransitionResult(false, 0, $"Failed to parse transitions response: {ex.Message}");
        }

        if (transitionId is null)
            return new JiraTransitionResult(false, 0, $"Transition to status '{targetStatusName}' not found in Jira for issue {issueKey}");

        // Step 3: POST the transition.
        var postRequest = BuildBasicAuthRequest(HttpMethod.Post, transitionsUrl, email, apiToken);
        var payload = JsonSerializer.Serialize(new { transition = new { id = transitionId } });
        postRequest.Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage postResponse;
        try
        {
            postResponse = await _httpClient.SendAsync(postRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, baseUrl, ex);
            return new JiraTransitionResult(false, 0, ex.Message);
        }

        if (postResponse.IsSuccessStatusCode)
            return new JiraTransitionResult(true, (int)postResponse.StatusCode, null);

        var postErrorBody = await postResponse.Content.ReadAsStringAsync(cancellationToken);
        LogTransitionFailed(_logger, issueKey, targetStatusName, (int)postResponse.StatusCode);
        return new JiraTransitionResult(false, (int)postResponse.StatusCode, postErrorBody);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch Jira project {ProjectKey}: HTTP {StatusCode}")]
    private static partial void LogProjectFetchFailed(ILogger logger, string projectKey, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deregister Jira webhook {WebhookId}: HTTP {StatusCode} — {ErrorBody}")]
    private static partial void LogDeregisterFailed(ILogger logger, string webhookId, int statusCode, string errorBody);

    [LoggerMessage(Level = LogLevel.Error, Message = "Network error communicating with Jira at {BaseUrl}")]
    private static partial void LogNetworkError(ILogger logger, string baseUrl, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch transitions for Jira issue {IssueKey}: HTTP {StatusCode}")]
    private static partial void LogTransitionFetchFailed(ILogger logger, string issueKey, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to transition Jira issue {IssueKey} to '{TargetStatus}': HTTP {StatusCode}")]
    private static partial void LogTransitionFailed(ILogger logger, string issueKey, string targetStatus, int statusCode);
}
