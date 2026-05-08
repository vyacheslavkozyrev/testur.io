using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

public class TestRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public required string JiraIssueKey { get; init; }
    public required string JiraIssueId { get; init; }
    public TestRunStatus Status { get; set; } = TestRunStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? SkipReason { get; set; }
    public string? DeliveryError { get; set; }
}
