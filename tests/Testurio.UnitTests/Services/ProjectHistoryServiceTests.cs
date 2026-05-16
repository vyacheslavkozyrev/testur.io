using Moq;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.UnitTests.Services;

public class ProjectHistoryServiceTests
{
    private readonly Mock<IStatsRepository> _statsRepository = new();
    private readonly ProjectHistoryService _sut;

    public ProjectHistoryServiceTests()
    {
        _sut = new ProjectHistoryService(_statsRepository.Object);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static RunHistoryItem MakeRun(
        string id = "result-1",
        string runId = "run-1",
        string storyTitle = "User story",
        string verdict = "PASSED",
        DateTimeOffset? createdAt = null) =>
        new(
            Id: id,
            RunId: runId,
            StoryTitle: storyTitle,
            Verdict: verdict,
            Recommendation: "approve",
            TotalApiScenarios: 2,
            PassedApiScenarios: 2,
            TotalUiE2eScenarios: 0,
            PassedUiE2eScenarios: 0,
            TotalDurationMs: 5000,
            CreatedAt: createdAt ?? DateTimeOffset.UtcNow);

    private static TrendPoint MakeTrendPoint(DateOnly date, int passed = 0, int failed = 0) =>
        new(date, passed, failed);

    private static TestResult MakeTestResult(
        string id = "result-1",
        string runId = "run-1",
        string projectId = "project-1",
        string userId = "user-1") =>
        new()
        {
            Id = id,
            RunId = runId,
            ProjectId = projectId,
            UserId = userId,
            StoryTitle = "User story",
            Verdict = "PASSED",
            Recommendation = "approve",
            TotalDurationMs = 5000,
        };

    // ─── GetHistoryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_WhenProjectNotFound_ReturnsNull()
    {
        _statsRepository
            .Setup(r => r.GetProjectHistoryAsync("user-1", "project-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RunHistoryItem> Runs, IReadOnlyList<TrendPoint> TrendPoints)? default);

        var result = await _sut.GetHistoryAsync("user-1", "project-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHistoryAsync_WhenDataReturned_MapsRunsAndTrendPoints()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var runs = (IReadOnlyList<RunHistoryItem>) new[]
        {
            MakeRun(id: "r2", createdAt: DateTimeOffset.UtcNow.AddHours(-1)),
            MakeRun(id: "r1", createdAt: DateTimeOffset.UtcNow.AddHours(-2)),
        };
        var trendPoints = (IReadOnlyList<TrendPoint>) new[] { MakeTrendPoint(today, passed: 2) };

        _statsRepository
            .Setup(r => r.GetProjectHistoryAsync("user-1", "project-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((runs, trendPoints));

        var result = await _sut.GetHistoryAsync("user-1", "project-1");

        Assert.NotNull(result);
        Assert.Equal(2, result.Runs.Count);
        // Ordering is preserved from repository (repository sorts by createdAt desc).
        Assert.Equal("r2", result.Runs[0].Id);
        Assert.Equal("r1", result.Runs[1].Id);
        Assert.Single(result.TrendPoints);
        Assert.Equal(2, result.TrendPoints[0].Passed);
    }

    [Fact]
    public async Task GetHistoryAsync_TrendPoints_ZeroFilledForDaysWithNoRuns()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var trendPoints = (IReadOnlyList<TrendPoint>) new[]
        {
            MakeTrendPoint(today.AddDays(-2), passed: 0, failed: 0),
            MakeTrendPoint(today.AddDays(-1), passed: 1, failed: 0),
            MakeTrendPoint(today,             passed: 0, failed: 0),
        };

        _statsRepository
            .Setup(r => r.GetProjectHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<RunHistoryItem>() as IReadOnlyList<RunHistoryItem>, trendPoints));

        var result = await _sut.GetHistoryAsync("user-1", "project-1");

        Assert.NotNull(result);
        // Zero-filled days are present.
        Assert.Equal(0, result.TrendPoints[0].Passed);
        Assert.Equal(0, result.TrendPoints[0].Failed);
        Assert.Equal(0, result.TrendPoints[2].Passed);
        Assert.Equal(0, result.TrendPoints[2].Failed);
    }

    // ─── GetRunDetailAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetRunDetailAsync_WhenRunNotFound_ReturnsNull()
    {
        _statsRepository
            .Setup(r => r.GetRunDetailAsync("user-1", "project-1", "run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestResult?) null);

        var result = await _sut.GetRunDetailAsync("user-1", "project-1", "run-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRunDetailAsync_WhenProjectIdMismatch_ReturnsNull()
    {
        // Repository already handles the mismatch check and returns null.
        _statsRepository
            .Setup(r => r.GetRunDetailAsync("user-1", "wrong-project", "run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestResult?) null);

        var result = await _sut.GetRunDetailAsync("user-1", "wrong-project", "run-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRunDetailAsync_WhenFound_MapsAllFields()
    {
        var scenario = new ScenarioSummary(
            Title: "POST /auth — returns 200",
            Passed: true,
            DurationMs: 320,
            ErrorSummary: null,
            TestType: "api",
            ScreenshotUris: Array.Empty<string>());

        var testResult = MakeTestResult() with
        {
            ScenarioResults = new[] { scenario },
            RawCommentMarkdown = "## Report\nPASSED",
        };

        _statsRepository
            .Setup(r => r.GetRunDetailAsync("user-1", "project-1", "run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        var result = await _sut.GetRunDetailAsync("user-1", "project-1", "run-1");

        Assert.NotNull(result);
        Assert.Equal("result-1", result.Id);
        Assert.Equal("run-1", result.RunId);
        Assert.Equal("PASSED", result.Verdict);
        Assert.Equal("approve", result.Recommendation);
        Assert.Equal(5000, result.TotalDurationMs);
        Assert.Single(result.ScenarioResults);
        Assert.Equal("POST /auth — returns 200", result.ScenarioResults[0].Title);
        Assert.Equal("## Report\nPASSED", result.RawCommentMarkdown);
    }
}
