using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Xunit;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Unit tests verifying that <see cref="DashboardService"/> resolves the daily limit from
/// <see cref="IQuotaPolicy"/> and passes it to <see cref="IStatsRepository.GetQuotaUsageAsync"/>
/// (feature 0021, AC-010, AC-011, AC-013).
/// </summary>
public class DashboardServiceQuotaTests
{
    private readonly Mock<IStatsRepository> _statsRepository = new();
    private readonly Mock<IQuotaPolicy> _quotaPolicy = new();
    private readonly Mock<IUserSubscriptionRepository> _subscriptionRepository = new();

    private DashboardService CreateSut() => new(
        _statsRepository.Object,
        _quotaPolicy.Object,
        _subscriptionRepository.Object);

    private static DashboardProjectSummary MakeProject() =>
        new("proj-1", "Test Project", "https://example.com", "API testing.", null);

    private void SetupEmptySummaries(string userId = "user-1")
    {
        _statsRepository
            .Setup(r => r.GetDashboardSummariesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardProjectSummary>());
    }

    private void SetupQuotaReturns(int usedToday = 0, int dailyLimit = 0)
    {
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaUsage(usedToday, dailyLimit, DateTimeOffset.UtcNow.AddDays(1)));
    }

    // ─── Active subscription → plan-tier limit ────────────────────────────────

    [Theory]
    [InlineData(SubscriptionPlan.TestJunior, 10)]
    [InlineData(SubscriptionPlan.TestPro, 30)]
    [InlineData(SubscriptionPlan.Team, 100)]
    [InlineData(SubscriptionPlan.Centurio, 500)]
    public async Task GetDashboardAsync_ActiveSubscription_PassesPlanTierLimitToRepository(
        SubscriptionPlan plan, int expectedLimit)
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = plan,
                Status = SubscriptionStatus.Active
            });
        _quotaPolicy.Setup(p => p.GetDailyLimit(plan)).Returns(expectedLimit);
        SetupEmptySummaries();
        SetupQuotaReturns(0, expectedLimit);

        var sut = CreateSut();
        await sut.GetDashboardAsync("user-1");

        // Verify the repo was called with the correct dailyLimit derived from the plan.
        _statsRepository.Verify(r => r.GetQuotaUsageAsync("user-1", expectedLimit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDashboardAsync_TrialingSubscription_UsesPlanTierLimit()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestPro,
                Status = SubscriptionStatus.Trialing
            });
        _quotaPolicy.Setup(p => p.GetDailyLimit(SubscriptionPlan.TestPro)).Returns(30);
        SetupEmptySummaries();
        SetupQuotaReturns(0, 30);

        var sut = CreateSut();
        await sut.GetDashboardAsync("user-1");

        _statsRepository.Verify(r => r.GetQuotaUsageAsync("user-1", 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── No subscription or expired → dailyLimit = 0 ─────────────────────────

    [Fact]
    public async Task GetDashboardAsync_NoSubscription_PassesZeroLimitToRepository()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);
        SetupEmptySummaries();
        SetupQuotaReturns(0, 0);

        var sut = CreateSut();
        await sut.GetDashboardAsync("user-1");

        _statsRepository.Verify(r => r.GetQuotaUsageAsync("user-1", 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDashboardAsync_ExpiredSubscription_PassesZeroLimitToRepository()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestPro,
                Status = SubscriptionStatus.Expired
            });
        SetupEmptySummaries();
        SetupQuotaReturns(0, 0);

        var sut = CreateSut();
        await sut.GetDashboardAsync("user-1");

        _statsRepository.Verify(r => r.GetQuotaUsageAsync("user-1", 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDashboardAsync_NoneSubscription_PassesZeroLimitToRepository()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.None
            });
        SetupEmptySummaries();
        SetupQuotaReturns(0, 0);

        var sut = CreateSut();
        await sut.GetDashboardAsync("user-1");

        _statsRepository.Verify(r => r.GetQuotaUsageAsync("user-1", 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Dashboard response reflects what the repository returns ─────────────

    [Fact]
    public async Task GetDashboardAsync_ReturnsQuotaFromRepository()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Active
            });
        _quotaPolicy.Setup(p => p.GetDailyLimit(SubscriptionPlan.TestJunior)).Returns(10);
        SetupEmptySummaries();
        _statsRepository
            .Setup(r => r.GetQuotaUsageAsync("user-1", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaUsage(7, 10, DateTimeOffset.UtcNow.AddDays(1)));

        var sut = CreateSut();
        var result = await sut.GetDashboardAsync("user-1");

        Assert.Equal(7, result.QuotaUsage.UsedToday);
        Assert.Equal(10, result.QuotaUsage.DailyLimit);
    }
}
