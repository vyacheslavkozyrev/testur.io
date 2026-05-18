namespace Testurio.Core.Interfaces;

/// <summary>
/// Result of an ADO work item state transition attempt (feature 0024).
/// </summary>
public sealed record ADOTransitionResult(bool IsSuccess, int StatusCode, string? ErrorDetail);

/// <summary>
/// Metadata about an Azure DevOps project returned by the test-connection call.
/// </summary>
public sealed record ADOProjectInfo(string Id, string Name, string State);

/// <summary>
/// Result of an ADO test-connection operation.
/// </summary>
public sealed record ADOConnectionTestResult(bool IsSuccess, int StatusCode, string? ErrorDetail);

/// <summary>
/// Client contract for Azure DevOps REST API operations required by the PM tool integration.
/// </summary>
public interface IADOClient
{
    /// <summary>
    /// Fetches project metadata from ADO to verify the org URL, project name, and token are valid.
    /// Returns null when the project was not found or the request failed.
    /// </summary>
    Task<ADOProjectInfo?> GetProjectAsync(
        string orgUrl,
        string projectName,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight connectivity check using the stored token.
    /// Returns a result indicating success, auth failure, or unreachable.
    /// </summary>
    Task<ADOConnectionTestResult> TestConnectionAsync(
        string orgUrl,
        string projectName,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a comment on an ADO work item.
    /// Returns the string comment ID assigned by ADO on success, or <c>null</c> when the post fails
    /// or the response body cannot be parsed.
    /// </summary>
    Task<string?> PostCommentAsync(
        string orgUrl,
        string projectName,
        int workItemId,
        string token,
        string commentBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters a previously registered service hook subscription from ADO.
    /// Silently succeeds if the subscription no longer exists.
    /// </summary>
    Task DeregisterWebhookAsync(
        string orgUrl,
        string subscriptionId,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions an ADO work item to the named state using a PATCH to the
    /// <c>System.State</c> field (<c>/_apis/wit/workitems/{id}?api-version=7.1</c>).
    /// Never throws — all errors are captured in the returned result.
    /// </summary>
    Task<ADOTransitionResult> TransitionWorkItemStateAsync(
        string orgUrl,
        string projectName,
        int workItemId,
        string token,
        string targetStateName,
        CancellationToken cancellationToken = default);
}
