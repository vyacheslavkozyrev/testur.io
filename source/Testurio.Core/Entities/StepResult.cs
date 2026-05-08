using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

public class StepResult
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string TestRunId { get; init; }
    public required string ScenarioId { get; init; }
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public required string StepTitle { get; init; }
    public StepStatus Status { get; set; }
    public int ExpectedStatusCode { get; init; }
    public int? ActualStatusCode { get; set; }
    public string? ExpectedResponseSchema { get; init; }
    public string? ActualResponseBody { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorDescription { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
