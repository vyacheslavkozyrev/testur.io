using Microsoft.Azure.Cosmos;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Implements <see cref="IStatsRepository"/> using Cosmos DB containers:
/// <c>Projects</c> (partition key: <c>userId</c>), <c>TestRuns</c> (partition key: <c>projectId</c>),
/// and <c>TestResults</c> (partition key: <c>userId</c>).
/// </summary>
public class StatsRepository : IStatsRepository
{
    private readonly Container _projectsContainer;
    private readonly Container _testRunsContainer;
    private readonly Container _testResultsContainer;

    public StatsRepository(CosmosClient cosmosClient, string databaseName)
    {
        _projectsContainer = cosmosClient.GetContainer(databaseName, "Projects");
        _testRunsContainer = cosmosClient.GetContainer(databaseName, "TestRuns");
        _testResultsContainer = cosmosClient.GetContainer(databaseName, "TestResults");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DashboardProjectSummary>> GetDashboardSummariesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch all active projects for this user (partition-scoped — no fan-out).
        var projectQuery = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)")
            .WithParameter("@userId", userId);

        var projects = new List<Project>();
        using var projectIterator = _projectsContainer.GetItemQueryIterator<Project>(
            projectQuery,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (projectIterator.HasMoreResults)
        {
            var page = await projectIterator.ReadNextAsync(cancellationToken);
            projects.AddRange(page);
        }

        if (projects.Count == 0)
            return Array.Empty<DashboardProjectSummary>();

        // 2. For each project, fetch its most recent test run (one per project, partition-scoped).
        var latestRuns = new Dictionary<string, LatestRunSummary>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            var runQuery = new QueryDefinition(
                "SELECT TOP 1 c.id, c.status, c.startedAt, c.completedAt FROM c " +
                "WHERE c.projectId = @projectId ORDER BY c.startedAt DESC")
                .WithParameter("@projectId", project.Id);

            using var runIterator = _testRunsContainer.GetItemQueryIterator<TestRunProjection>(
                runQuery,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(project.Id),
                    MaxItemCount = 1,
                });

            while (runIterator.HasMoreResults)
            {
                var page = await runIterator.ReadNextAsync(cancellationToken);
                var run = page.FirstOrDefault();
                if (run is not null)
                {
                    latestRuns[project.Id] = new LatestRunSummary(
                        RunId: run.Id,
                        Status: MapStatus(run.Status),
                        StartedAt: run.StartedAt ?? run.CreatedAt,
                        CompletedAt: run.CompletedAt);
                }
                break; // TOP 1 — only first page needed
            }
        }

        // 3. Build summaries and sort: runs-present first by startedAt desc, then no-run projects alpha.
        var withRun = projects
            .Where(p => latestRuns.ContainsKey(p.Id))
            .OrderByDescending(p => latestRuns[p.Id].StartedAt)
            .Select(p => new DashboardProjectSummary(p.Id, p.Name, p.ProductUrl, p.TestingStrategy, latestRuns[p.Id]));

        var withoutRun = projects
            .Where(p => !latestRuns.ContainsKey(p.Id))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new DashboardProjectSummary(p.Id, p.Name, p.ProductUrl, p.TestingStrategy, null));

        return withRun.Concat(withoutRun).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<QuotaUsage> GetQuotaUsageAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Compute "today" window in UTC (midnight-to-midnight).
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1); // = resetsAt (next midnight UTC)

        // Count completed runs started today across all projects for this user.
        // TestRuns container partition key is projectId, so this requires cross-partition fan-out.
        // Acceptable here: stats query, not on the hot write path.
        var countQuery = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c " +
            "WHERE c.userId = @userId AND c.startedAt >= @start AND c.startedAt < @end")
            .WithParameter("@userId", userId)
            .WithParameter("@start", todayStart.ToString("o"))
            .WithParameter("@end", todayEnd.ToString("o"));

        var usedToday = 0;
        using var countIterator = _testRunsContainer.GetItemQueryIterator<int>(countQuery);
        while (countIterator.HasMoreResults)
        {
            var page = await countIterator.ReadNextAsync(cancellationToken);
            foreach (var count in page)
                usedToday += count;
        }

        // DailyLimit is 0 for users without a subscription plan (feature 0021 will enforce limits).
        // For now the repository always returns 0 daily limit as quota enforcement is not yet implemented.
        const int dailyLimit = 0;

        return new QuotaUsage(usedToday, dailyLimit, todayEnd);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<RunHistoryItem> Runs, IReadOnlyList<TrendPoint> TrendPoints)?> GetProjectHistoryAsync(
        string userId,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate ownership — point-read is cheap and enforces multi-tenant isolation.
        try
        {
            await _projectsContainer.ReadItemAsync<Project>(
                projectId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        // 2. Fetch all non-deleted TestResult documents for this project, sorted by createdAt desc.
        //    TestResults partition key is userId — no cross-partition fan-out needed.
        var resultQuery = new QueryDefinition(
            "SELECT * FROM c " +
            "WHERE c.userId = @userId AND c.projectId = @projectId " +
            "AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) " +
            "ORDER BY c.createdAt DESC")
            .WithParameter("@userId", userId)
            .WithParameter("@projectId", projectId);

        var results = new List<TestResult>();
        using var resultIterator = _testResultsContainer.GetItemQueryIterator<TestResult>(
            resultQuery,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (resultIterator.HasMoreResults)
        {
            var page = await resultIterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        // 3. Project each TestResult → RunHistoryItem.
        var runs = results
            .Select(r => new RunHistoryItem(
                Id: r.Id,
                RunId: r.RunId,
                StoryTitle: r.StoryTitle,
                Verdict: r.Verdict,
                Recommendation: r.Recommendation,
                TotalApiScenarios: r.ScenarioResults.Count(s => s.TestType == "api"),
                PassedApiScenarios: r.ScenarioResults.Count(s => s.TestType == "api" && s.Passed),
                TotalUiE2eScenarios: r.ScenarioResults.Count(s => s.TestType == "ui_e2e"),
                PassedUiE2eScenarios: r.ScenarioResults.Count(s => s.TestType == "ui_e2e" && s.Passed),
                TotalDurationMs: r.TotalDurationMs,
                CreatedAt: r.CreatedAt))
            .ToList()
            .AsReadOnly();

        // 4. Compute 90 trend-point buckets (today-89 through today, UTC), zero-filled.
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var passedByDate = results
            .Where(r => r.Verdict == "PASSED")
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());
        var failedByDate = results
            .Where(r => r.Verdict == "FAILED")
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());

        var trendPoints = Enumerable.Range(0, 90)
            .Select(i =>
            {
                var date = today.AddDays(-(89 - i));
                passedByDate.TryGetValue(date, out var passed);
                failedByDate.TryGetValue(date, out var failed);
                return new TrendPoint(date, passed, failed);
            })
            .ToList()
            .AsReadOnly();

        return (runs, trendPoints);
    }

    /// <inheritdoc/>
    public async Task<TestResult?> GetRunDetailAsync(
        string userId,
        string projectId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate project ownership.
        try
        {
            await _projectsContainer.ReadItemAsync<Project>(
                projectId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        // 2. Find the TestResult whose RunId matches (TestResult.Id ≠ runId; RunId field stores the TestRun id).
        //    We query by runId field rather than document id because the plan uses runId as the URL param.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND c.runId = @runId " +
            "AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)")
            .WithParameter("@userId", userId)
            .WithParameter("@runId", runId);

        using var iterator = _testResultsContainer.GetItemQueryIterator<TestResult>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var result in page)
            {
                // Verify the result belongs to the requested project.
                if (result.ProjectId != projectId)
                    return null;
                return result;
            }
        }

        return null;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static RunStatus MapStatus(TestRunStatus status) => status switch
    {
        TestRunStatus.Pending                => RunStatus.Queued,
        TestRunStatus.Active                 => RunStatus.Running,
        TestRunStatus.Completed              => RunStatus.Passed,
        TestRunStatus.Failed                 => RunStatus.Failed,
        TestRunStatus.ReportDeliveryFailed   => RunStatus.Failed,
        TestRunStatus.Skipped                => RunStatus.Cancelled,
        _                                    => RunStatus.Failed,
    };

    /// <summary>Minimal projection for the latest-run query — avoids deserialising full TestRun documents.</summary>
    private sealed class TestRunProjection
    {
        public string Id { get; init; } = string.Empty;
        public TestRunStatus Status { get; init; }
        public DateTimeOffset? StartedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
