using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure.ADO.Models;

namespace Testurio.Infrastructure.ADO;

public partial class ADOClient : IADOClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ADOClient> _logger;

    public ADOClient(HttpClient httpClient, ILogger<ADOClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ADOProjectInfo?> GetProjectAsync(
        string orgUrl,
        string projectName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var url = $"{orgUrl.TrimEnd('/')}/_apis/projects/{Uri.EscapeDataString(projectName)}?api-version=7.1";
        var request = BuildRequest(HttpMethod.Get, url, token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, orgUrl, ex);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogProjectFetchFailed(_logger, projectName, (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var project = JsonSerializer.Deserialize<ADOProjectResponse>(body, JsonOptions);

        if (project?.Id is null || project.Name is null)
            return null;

        return new ADOProjectInfo(project.Id, project.Name, project.State ?? "unknown");
    }

    public async Task<ADOConnectionTestResult> TestConnectionAsync(
        string orgUrl,
        string projectName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var url = $"{orgUrl.TrimEnd('/')}/_apis/projects/{Uri.EscapeDataString(projectName)}?api-version=7.1";
        var request = BuildRequest(HttpMethod.Get, url, token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNetworkError(_logger, orgUrl, ex);
            return new ADOConnectionTestResult(false, 0, ex.Message);
        }

        if (response.IsSuccessStatusCode)
            return new ADOConnectionTestResult(true, (int)response.StatusCode, null);

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new ADOConnectionTestResult(false, (int)response.StatusCode, errorBody);

        return new ADOConnectionTestResult(false, (int)response.StatusCode, errorBody);
    }

    public async Task<bool> PostCommentAsync(
        string orgUrl,
        string projectName,
        int workItemId,
        string token,
        string commentBody,
        CancellationToken cancellationToken = default)
    {
        var url = $"{orgUrl.TrimEnd('/')}/{Uri.EscapeDataString(projectName)}/_apis/wit/workItems/{workItemId}/comments?api-version=7.1-preview.3";
        var request = BuildRequest(HttpMethod.Post, url, token);
        var payload = new { text = commentBody };
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
            LogNetworkError(_logger, orgUrl, ex);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogCommentFailed(_logger, workItemId, (int)response.StatusCode, errorBody);
            return false;
        }

        return true;
    }

    public async Task DeregisterWebhookAsync(
        string orgUrl,
        string subscriptionId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var url = $"{orgUrl.TrimEnd('/')}/_apis/hooks/subscriptions/{subscriptionId}?api-version=7.1";
        var request = BuildRequest(HttpMethod.Delete, url, token);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                LogDeregisterFailed(_logger, subscriptionId, (int)response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            // Deregistration failures are logged but never surface as errors to callers (AC-048).
            LogNetworkError(_logger, orgUrl, ex);
        }
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        // ADO accepts a PAT encoded as Basic auth with an empty username.
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{token}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch ADO project {ProjectName}: HTTP {StatusCode}")]
    private static partial void LogProjectFetchFailed(ILogger logger, string projectName, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to post comment on ADO work item {WorkItemId}: HTTP {StatusCode} — {ErrorBody}")]
    private static partial void LogCommentFailed(ILogger logger, int workItemId, int statusCode, string errorBody);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deregister ADO webhook subscription {SubscriptionId}: HTTP {StatusCode} — {ErrorBody}")]
    private static partial void LogDeregisterFailed(ILogger logger, string subscriptionId, int statusCode, string errorBody);

    [LoggerMessage(Level = LogLevel.Error, Message = "Network error communicating with ADO at {OrgUrl}")]
    private static partial void LogNetworkError(ILogger logger, string orgUrl, Exception ex);
}
