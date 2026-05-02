namespace Testurio.Core.Models;

public class TestRunJobMessage
{
    public required string TestRunId { get; init; }
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public required string JiraIssueKey { get; init; }
    public required string JiraIssueId { get; init; }
}
