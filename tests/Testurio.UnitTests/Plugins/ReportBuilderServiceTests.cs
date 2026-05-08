using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Plugins.ReportWriterPlugin;

namespace Testurio.UnitTests.Plugins;

public class ReportBuilderServiceTests
{
    private readonly ReportBuilderService _sut = new();

    private static TestRun MakeRun(TestRunStatus status = TestRunStatus.Completed) => new()
    {
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = status,
        CompletedAt = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero)
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

    private static StepResult MakeStep(
        string scenarioId,
        string title,
        StepStatus status,
        int expectedStatus = 200,
        int? actualStatus = 200,
        long durationMs = 150,
        string? errorDescription = null) => new()
    {
        TestRunId = "run1",
        ScenarioId = scenarioId,
        ProjectId = "proj1",
        UserId = "user1",
        StepTitle = title,
        Status = status,
        ExpectedStatusCode = expectedStatus,
        ActualStatusCode = actualStatus,
        DurationMs = durationMs,
        ErrorDescription = errorDescription
    };

    [Fact]
    public void Build_PassedRun_ContainsSummaryHeader()
    {
        var run = MakeRun(TestRunStatus.Completed);
        var scenario = MakeScenario("s1", "Login flow");
        var step = MakeStep("s1", "POST /api/login", StepStatus.Passed);

        var result = _sut.Build(run, new[] { scenario }, new[] { step });

        Assert.Contains("Passed", result);
        Assert.Contains("2026-05-07", result);
        Assert.Contains("1 total", result);
        Assert.Contains("1 passed", result);
        Assert.Contains("0 failed", result);
    }

    [Fact]
    public void Build_FailedRun_ContainsFailuresSectionBeforeBreakdown()
    {
        var run = MakeRun(TestRunStatus.Failed);
        var scenario = MakeScenario("s1", "Create order");
        var step = MakeStep("s1", "POST /api/orders", StepStatus.Failed, expectedStatus: 201, actualStatus: 400);

        var result = _sut.Build(run, new[] { scenario }, new[] { step });

        Assert.Contains("Failures", result);
        Assert.Contains("Full Scenario Breakdown", result);
        // Failures section must appear before the full breakdown
        Assert.True(result.IndexOf("Failures", StringComparison.Ordinal) < result.IndexOf("Full Scenario Breakdown", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_PassedRun_DoesNotContainFailuresSection()
    {
        var run = MakeRun(TestRunStatus.Completed);
        var scenario = MakeScenario("s1", "Get user");
        var step = MakeStep("s1", "GET /api/user/1", StepStatus.Passed);

        var result = _sut.Build(run, new[] { scenario }, new[] { step });

        Assert.DoesNotContain("*Failures*", result);
    }

    [Fact]
    public void Build_FailedRun_FailuresSectionListsScenarioAndStep()
    {
        var run = MakeRun(TestRunStatus.Failed);
        var scenario = MakeScenario("s1", "Delete resource");
        var step = MakeStep("s1", "DELETE /api/resource/1", StepStatus.Failed, expectedStatus: 204, actualStatus: 403);

        var result = _sut.Build(run, new[] { scenario }, new[] { step });

        Assert.Contains("Delete resource", result);
        Assert.Contains("DELETE /api/resource/1", result);
        Assert.Contains("Expected status: 204", result);
        Assert.Contains("Actual status: 403", result);
    }

    [Fact]
    public void Build_TimeoutStep_IncludesElapsedDuration()
    {
        var run = MakeRun(TestRunStatus.Failed);
        var scenario = MakeScenario("s1", "Slow endpoint");
        var step = MakeStep("s1", "GET /api/slow", StepStatus.Timeout, durationMs: 10_000);

        var result = _sut.Build(run, new[] { scenario }, new[] { step });

        // Timeout reason and elapsed duration must appear in failures section
        Assert.Contains("Timeout after 10000 ms", result);
        // Also appears in full breakdown
        Assert.Contains("Timed out after 10000 ms", result);
    }

    [Fact]
    public void Build_ErrorStep_IncludesErrorDescription()
    {
        var run = MakeRun(TestRunStatus.Failed);
        var scenario = MakeScenario("s1", "Malformed request");
        var step = MakeStep("s1", "POST /api/items", StepStatus.Error, errorDescription: "invalid request definition");

        var result = _sut.Build(run, new[] { scenario }, new[] { step });

        Assert.Contains("invalid request definition", result);
    }

    [Fact]
    public void Build_FullBreakdown_ListsAllScenariosAndSteps()
    {
        var run = MakeRun(TestRunStatus.Completed);
        var s1 = MakeScenario("s1", "Scenario One");
        var s2 = MakeScenario("s2", "Scenario Two");
        var step1 = MakeStep("s1", "Step A", StepStatus.Passed);
        var step2 = MakeStep("s2", "Step B", StepStatus.Passed);

        var result = _sut.Build(run, new[] { s1, s2 }, new[] { step1, step2 });

        Assert.Contains("Scenario One", result);
        Assert.Contains("Scenario Two", result);
        Assert.Contains("Step A", result);
        Assert.Contains("Step B", result);
    }

    [Fact]
    public void Build_WithLogSection_AppendsLogSectionAtEnd()
    {
        var run = MakeRun(TestRunStatus.Completed);
        var scenario = MakeScenario("s1", "Flow");
        var step = MakeStep("s1", "GET /api/ping", StepStatus.Passed);
        const string logSection = "Execution log content here";

        var result = _sut.Build(run, new[] { scenario }, new[] { step }, logSection);

        Assert.Contains(logSection, result);
        // Log section must appear after the full breakdown
        Assert.True(result.IndexOf(logSection, StringComparison.Ordinal) > result.IndexOf("Full Scenario Breakdown", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithoutLogSection_DoesNotDuplicateSeparator()
    {
        var run = MakeRun(TestRunStatus.Completed);
        var scenario = MakeScenario("s1", "Flow");
        var step = MakeStep("s1", "GET /api/ping", StepStatus.Passed);

        var result = _sut.Build(run, new[] { scenario }, new[] { step }, logSection: null);

        // "---" should appear exactly once (after the summary header) for a passed run with no log section
        var occurrences = System.Text.RegularExpressions.Regex.Matches(result, @"---").Count;
        Assert.Equal(1, occurrences);
    }
}
