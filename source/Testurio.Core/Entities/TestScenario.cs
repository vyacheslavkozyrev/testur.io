using Testurio.Core.Models;

namespace Testurio.Core.Entities;

public class TestScenario
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string TestRunId { get; init; }
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<TestScenarioStep> Steps { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
