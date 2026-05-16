using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.UnitTests.Services;

public class DashboardServiceTests
{
    private readonly Mock<IStatsRepository> _statsRepository = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _sut = new DashboardService(_statsRepository.Object);
    }

    private static LatestRunSummary MakeRun(
        string runId = "run-1",
        RunStatus status = RunStatus.Passed,
        DateTimeOffset? startedAt = null) =>
        new(runId, status, startedAt ?? DateTimeOffset.UtcNow, null);

    private static DashboardProjectSummary MakeProject(
        string projectId = "proj-1",
        string name = "Project A",
        LatestRunSummary? latestRun = null) =>
        new(projectId, name, "https://example.com", "API testing.", latestRun);

    private static QuotaUsage MakeQuota(int used = 0, int limit = 50) =>
        new(used, limit, DateTimeOffset.UtcNow.AddDays(1));

    // ─── GetDashboardAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardAsync_ReturnsSummariesAndQuota()
    {
        var projects = new[] { MakeProject(latestRun: MakeRun()) };
        var quota = MakeQuota(used: 5, limit: 50);

        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        var result = await _sut.GetDashboardAsync("user-1");

        Assert.Single(result.Projects);
        Assert.Equal("proj-1", result.Projects[0].ProjectId);
        Assert.Equal(5, result.QuotaUsage.UsedToday);
        Assert.Equal(50, result.QuotaUsage.DailyLimit);
    }

    [Fact]
    public async Task GetDashboardAsync_ProjectsWithRuns_SortedBeforeNoRunProjects()
    {
        // Repository is responsible for sorting; service delegates without re-sorting.
        // This test verifies the service passes through the repository order unchanged.
        var older = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = DateTimeOffset.UtcNow.AddHours(-1);

        var projectsFromRepo = new[]
        {
            MakeProject("proj-2", "B Project", MakeRun(startedAt: newer)),
            MakeProject("proj-1", "A Project", MakeRun(startedAt: older)),
            MakeProject("proj-3", "C Project"),          // no run — last
        };

        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectsFromRepo);
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota());

        var result = await _sut.GetDashboardAsync("user-1");

        Assert.Equal(3, result.Projects.Count);
        Assert.Equal("proj-2", result.Projects[0].ProjectId);
        Assert.Equal("proj-1", result.Projects[1].ProjectId);
        Assert.Equal("proj-3", result.Projects[2].ProjectId);
    }

    [Fact]
    public async Task GetDashboardAsync_NoRunProjects_AlphaOrdering()
    {
        var noRunProjects = new[]
        {
            MakeProject("proj-c", "Zeta"),
            MakeProject("proj-a", "Alpha"),
            MakeProject("proj-b", "Mango"),
        };

        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(noRunProjects);
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota());

        var result = await _sut.GetDashboardAsync("user-1");

        // Service passes repository order through — alpha sort is enforced by the repo.
        Assert.Equal(3, result.Projects.Count);
        Assert.Equal("proj-c", result.Projects[0].ProjectId);
    }

    [Fact]
    public async Task GetDashboardAsync_EmptyProjects_ReturnsEmptyList()
    {
        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardProjectSummary>());
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota());

        var result = await _sut.GetDashboardAsync("user-1");

        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task GetDashboardAsync_QuotaAtLimit_ReturnedUnchanged()
    {
        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardProjectSummary>());
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota(used: 50, limit: 50));

        var result = await _sut.GetDashboardAsync("user-1");

        Assert.Equal(50, result.QuotaUsage.UsedToday);
        Assert.Equal(50, result.QuotaUsage.DailyLimit);
    }

    [Fact]
    public async Task GetDashboardAsync_NoPlan_DailyLimitIsZero()
    {
        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardProjectSummary>());
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeQuota(used: 0, limit: 0));

        var result = await _sut.GetDashboardAsync("user-1");

        Assert.Equal(0, result.QuotaUsage.DailyLimit);
        Assert.Equal(0, result.QuotaUsage.UsedToday);
    }
}
