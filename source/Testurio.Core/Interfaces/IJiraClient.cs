namespace Testurio.Core.Interfaces;

/// <summary>
/// Metadata about a Jira project returned by the test-connection call.
/// </summary>
public sealed record JiraProjectInfo(string Id, string Key, string Name);

/// <summary>
/// Result of a Jira test-connection operation.
/// </summary>
public sealed record JiraConnectionTestResult(bool IsSuccess, int StatusCode, string? ErrorDetail);

/// <summary>
/// Client contract for Jira REST API operations required by the PM tool integration.
/// Extends the existing IJiraApiClient (comment posting) with project-level and
/// webhook management operations.
/// </summary>
public interface IJiraClient
{
    /// <summary>
    /// Fetches project metadata from Jira to verify the base URL, project key, and credentials.
    /// Returns null when the project was not found or the request failed.
    /// </summary>
    Task<JiraProjectInfo?> GetProjectAsync(
        string baseUrl,
        string projectKey,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Overload for PAT-based auth.
    /// </summary>
    Task<JiraProjectInfo?> GetProjectWithPatAsync(
        string baseUrl,
        string projectKey,
        string pat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight connectivity check using the stored credentials.
    /// Returns a result indicating success, auth failure, or unreachable.
    /// </summary>
    Task<JiraConnectionTestResult> TestConnectionAsync(
        string baseUrl,
        string projectKey,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Overload for PAT-based auth.
    /// </summary>
    Task<JiraConnectionTestResult> TestConnectionWithPatAsync(
        string baseUrl,
        string projectKey,
        string pat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a comment on a Jira issue.
    /// </summary>
    Task<JiraCommentResult> PostCommentAsync(
        string baseUrl,
        string issueKey,
        string email,
        string apiToken,
        string commentBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters a previously registered webhook from Jira.
    /// Silently succeeds if the webhook no longer exists.
    /// </summary>
    Task DeregisterWebhookAsync(
        string baseUrl,
        string webhookId,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default);
}
