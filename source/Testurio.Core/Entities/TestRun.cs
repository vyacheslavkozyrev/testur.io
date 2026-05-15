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
    /// <summary>Blob Storage URI of the rendered report for this run. Populated by ReportWriterPlugin (feature 0009).</summary>
    public string? ReportBlobUri { get; set; }
    /// <summary>Warning recorded when the custom template blob could not be fetched and the built-in default was used instead.</summary>
    public string? ReportTemplateWarning { get; set; }
}
