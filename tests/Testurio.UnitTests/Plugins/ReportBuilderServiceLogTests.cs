using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Plugins.ReportWriterPlugin;

namespace Testurio.UnitTests.Plugins;

/// <summary>
/// Unit tests covering the log section extension to <see cref="ReportBuilderService"/>
/// (feature 0005 — AC-012 – AC-015).
/// Isolated from existing <see cref="ReportBuilderServiceTests"/> to keep test files focused.
/// </summary>
public class ReportBuilderServiceLogTests
{
    private readonly ReportBuilderService _sut = new();

    private static TestRun MakeRun(TestRunStatus status = TestRunStatus.Completed) => new()
    {
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = status,
        CompletedAt = new DateTimeOffset(2026, 5, 8, 10, 0, 0, TimeSpan.Zero)
    };

    private static TestScenario MakeScenario(string id, string title) => new()
    {
        Id = id,
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        Title = title,
        Steps = new List<TestScenarioStep>()
    };

    private static StepResult MakeStep(string scenarioId, StepStatus status = StepStatus.Passed) => new()
    {
        TestRunId = "run1",
        ScenarioId = scenarioId,
        ProjectId = "proj1",
        UserId = "user1",
        StepTitle = "GET /api/items",
        Status = status,
        ExpectedStatusCode = 200,
        ActualStatusCode = 200,
        DurationMs = 50
    };

    private static ExecutionLogEntry MakeLogEntry(
        string scenarioId = "s1",
        int stepIndex = 0,
        string stepTitle = "GET /api/items",
        string? responseBodyInline = "{\"id\":1}",
        string? responseBlobUrl = null,
        bool responseTruncated = false,
        int? responseStatusCode = 200) => new()
    {
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        ScenarioId = scenarioId,
        StepIndex = stepIndex,
        StepTitle = stepTitle,
        HttpMethod = "GET",
        RequestUrl = "https://app.example.com/api/items",
        ResponseStatusCode = responseStatusCode,
        ResponseBodyInline = responseBodyInline,
        ResponseBodyBlobUrl = responseBlobUrl,
        ResponseTruncated = responseTruncated,
        DurationMs = 50
    };

    // — AC-012: log included for every run regardless of outcome —

    [Fact]
    public void BuildLogSection_PassedRun_IncludesLogBlocksForAllSteps()
    {
        var logEntry = MakeLogEntry();
        var scenario = MakeScenario("s1", "Add item to cart");

        var logSection = _sut.BuildLogSection(new[] { logEntry }, new[] { scenario });

        Assert.NotEmpty(logSection);
        Assert.Contains("Execution Logs", logSection);
        Assert.Contains("GET /api/items", logSection);
    }

    [Fact]
    public void BuildLogSection_FailedRun_IncludesLogBlocksForAllSteps()
    {
        // Log section content is independent of run status (AC-015).
        var logEntry = MakeLogEntry(responseStatusCode: 500);
        var scenario = MakeScenario("s1", "Delete resource");

        var logSection = _sut.BuildLogSection(new[] { logEntry }, new[] { scenario });

        Assert.NotEmpty(logSection);
        Assert.Contains("GET /api/items", logSection);
        Assert.Contains("500", logSection);
    }

    // — AC-013: request and response rendered as Jira code blocks —

    [Fact]
    public void BuildLogSection_InlineBody_RendersRequestAndResponseInCodeBlocks()
    {
        var logEntry = MakeLogEntry(responseBodyInline: "{\"id\":42}");
        var scenario = MakeScenario("s1", "Get item");

        var logSection = _sut.BuildLogSection(new[] { logEntry }, new[] { scenario });

        // Jira code block markers must be present for both request and response.
        Assert.Contains("{code}", logSection);
        Assert.Contains("GET https://app.example.com/api/items", logSection);
        Assert.Contains("{\"id\":42}", logSection);
        Assert.Contains("Status: 200", logSection);
    }

    // — AC-014: blob URL included instead of inline body —

    [Fact]
    public void BuildLogSection_BlobStoredBody_IncludesBlobUrlInsteadOfContent()
    {
        var entry = MakeLogEntry(
            responseBodyInline: null,
            responseBlobUrl: "https://blob.example.com/logs/run1/s1/0.txt");
        var scenario = MakeScenario("s1", "Flow");

        var logSection = _sut.BuildLogSection(new[] { entry }, new[] { scenario });

        Assert.Contains("https://blob.example.com/logs/run1/s1/0.txt", logSection);
        // Actual body content must not appear — only the URL.
        Assert.DoesNotContain("{\"id\":", logSection);
    }

    // — AC-008 / truncation notice —

    [Fact]
    public void BuildLogSection_TruncatedBody_ShowsTruncationNotice()
    {
        var truncatedContent = new string('x', 100);
        var entry = MakeLogEntry(
            responseBodyInline: truncatedContent,
            responseBlobUrl: null,
            responseTruncated: true);
        var scenario = MakeScenario("s1", "Large response");

        var logSection = _sut.BuildLogSection(new[] { entry }, new[] { scenario });

        Assert.Contains("truncated", logSection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(truncatedContent, logSection);
    }

    // — AC-012: log section appears after full breakdown when passed to Build() —

    [Fact]
    public void Build_WithLogSectionFromBuildLogSection_AppendsAfterBreakdown()
    {
        var run = MakeRun();
        var scenario = MakeScenario("s1", "Flow");
        var step = MakeStep("s1", StepStatus.Passed);
        var logEntry = MakeLogEntry();

        var logSection = _sut.BuildLogSection(new[] { logEntry }, new[] { scenario });
        var comment = _sut.Build(run, new[] { scenario }, new[] { step }, logSection);

        Assert.Contains("Execution Logs", comment);
        // Log section must appear after the full breakdown.
        Assert.True(
            comment.IndexOf("Execution Logs", StringComparison.Ordinal) >
            comment.IndexOf("Full Scenario Breakdown", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildLogSection_EmptyLogEntries_ReturnsEmptyString()
    {
        var scenario = MakeScenario("s1", "Flow");

        var logSection = _sut.BuildLogSection(Array.Empty<ExecutionLogEntry>(), new[] { scenario });

        Assert.Equal(string.Empty, logSection);
    }

    [Fact]
    public void BuildLogSection_MultipleSteps_OrderedByStepIndex()
    {
        var entry0 = MakeLogEntry(stepIndex: 0, stepTitle: "First Step");
        var entry1 = MakeLogEntry(stepIndex: 1, stepTitle: "Second Step");
        var scenario = MakeScenario("s1", "Multi-step flow");

        var logSection = _sut.BuildLogSection(new[] { entry1, entry0 }, new[] { scenario });

        var indexFirst = logSection.IndexOf("First Step", StringComparison.Ordinal);
        var indexSecond = logSection.IndexOf("Second Step", StringComparison.Ordinal);
        Assert.True(indexFirst < indexSecond, "Step 0 must appear before step 1 in the log section");
    }
}
