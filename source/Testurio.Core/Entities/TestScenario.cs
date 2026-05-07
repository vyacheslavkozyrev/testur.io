using Testurio.Core.Models;

namespace Testurio.Core.Entities;

public class TestScenario
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string TestRunId { get; init; }
    public required string ProjectId { get; init; }
    /// <summary>
    /// The owning user's ID. Stored for tenant-scoped queries without a join to the Projects container.
    /// </summary>
    public required string UserId { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<TestScenarioStep> Steps { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
