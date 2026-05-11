namespace Testurio.Core.Interfaces;

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
    /// </summary>
    Task<bool> PostCommentAsync(
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
}
