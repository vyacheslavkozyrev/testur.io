namespace Testurio.Core.Entities;

/// <summary>
/// Raw execution evidence for a single step within a test run.
/// Stored separately from <see cref="StepResult"/> so diagnostics data
/// does not bloat the structured pass/fail record.
/// </summary>
public class ExecutionLogEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Run that owns this log entry — used as a cascade-delete key.</summary>
    public required string TestRunId { get; init; }

    /// <summary>Partition key — mirrors the parent run's partition key.</summary>
    public required string ProjectId { get; init; }

    public required string UserId { get; init; }

    public required string ScenarioId { get; init; }

    /// <summary>Zero-based index of the step within its scenario.</summary>
    public required int StepIndex { get; init; }

    public required string StepTitle { get; init; }

    /// <summary>UTC timestamp when the step execution was attempted.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // — Request —

    public required string HttpMethod { get; init; }

    public required string RequestUrl { get; init; }

    /// <summary>All request headers serialised as name→value pairs.</summary>
    public Dictionary<string, string> RequestHeaders { get; init; } = new();

    /// <summary>Raw request body; null if the step had no body.</summary>
    public string? RequestBody { get; init; }

    // — Response —

    /// <summary>HTTP response status code; null on timeout or pre-send error.</summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>All response headers serialised as name→value pairs.</summary>
    public Dictionary<string, string> ResponseHeaders { get; init; } = new();

    /// <summary>
    /// Response body content when stored inline (≤ 10 KB).
    /// Null when the body is stored in blob storage — see <see cref="ResponseBodyBlobUrl"/>.
    /// </summary>
    public string? ResponseBodyInline { get; set; }

    /// <summary>
    /// Reference URL to the blob-stored response body when the body exceeded 10 KB.
    /// Null when the body is stored inline — see <see cref="ResponseBodyInline"/>.
    /// </summary>
    public string? ResponseBodyBlobUrl { get; set; }

    // — Outcome —

    public required long DurationMs { get; init; }

    /// <summary>Execution error or timeout detail; null on success.</summary>
    public string? ErrorDetail { get; set; }

    /// <summary>
    /// True when the response body was truncated to 10 KB because the blob upload failed.
    /// </summary>
    public bool ResponseTruncated { get; set; }
}
