namespace Testurio.Core.Interfaces;

/// <summary>
/// Result of a Jira comment-post attempt.
/// Carries the HTTP status code and error body on failure so callers can record
/// diagnostic detail against the run (AC-014).
/// </summary>
public sealed class JiraCommentResult
{
    public bool IsSuccess { get; private init; }

    /// <summary>HTTP status code returned by Jira (0 if a network error prevented a response).</summary>
    public int StatusCode { get; private init; }

    /// <summary>Response body or network error message on failure; null on success.</summary>
    public string? ErrorDetail { get; private init; }

    private JiraCommentResult() { }

    public static JiraCommentResult Success() => new() { IsSuccess = true };

    public static JiraCommentResult Failure(int statusCode, string errorDetail) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorDetail = errorDetail };
}

public interface IJiraApiClient
{
    Task<JiraCommentResult> PostCommentAsync(
        string baseUrl,
        string issueKey,
        string email,
        string apiToken,
        string commentBody,
        CancellationToken cancellationToken = default);
}
