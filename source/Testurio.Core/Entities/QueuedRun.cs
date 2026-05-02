namespace Testurio.Core.Entities;

public class QueuedRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public required string JiraIssueKey { get; init; }
    public required string JiraIssueId { get; init; }
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
    public int Position { get; set; }
}
