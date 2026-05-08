using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

public class StepResult
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>The project ID — used as the Cosmos DB partition key.</summary>
    public required string ProjectId { get; init; }

    public required string TestRunId { get; init; }
    public required string ScenarioId { get; init; }

    /// <summary>Human-readable label copied from the scenario step description.</summary>
    public required string StepTitle { get; init; }

    public StepStatus Status { get; set; } = StepStatus.Skipped;

    /// <summary>Actual HTTP status code returned by the product API (null on timeout or error).</summary>
    public int? ActualStatusCode { get; set; }

    /// <summary>Actual response body returned by the product API (null on timeout or error).</summary>
    public string? ActualResponseBody { get; set; }

    /// <summary>Actual response headers returned by the product API (null on timeout or error).</summary>
    public Dictionary<string, string>? ActualResponseHeaders { get; set; }

    /// <summary>The expected HTTP status code defined in the scenario step.</summary>
    public int? ExpectedStatusCode { get; set; }

    /// <summary>The expected response body schema defined in the scenario step (JSON Schema string).</summary>
    public string? ExpectedResponseSchema { get; set; }

    /// <summary>Step execution duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Human-readable failure or error message (null when the step passed).</summary>
    public string? FailureMessage { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
