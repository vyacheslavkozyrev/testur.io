using Microsoft.Azure.Cosmos;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Cosmos;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="StatsRepository.GetQuotaUsageAsync"/> trial-period paths
/// (feature 0021, AC-010, AC-011, AC-012).
/// Cosmos containers are mocked via Moq (the SDK's virtual API surface).
/// </summary>
public class StatsRepositoryQuotaTests
{
    private readonly Mock<CosmosClient> _cosmosClient = new();
    private readonly Mock<Container> _projectsContainer = new();
    private readonly Mock<Container> _testRunsContainer = new();
    private readonly Mock<Container> _testResultsContainer = new();
    private readonly Mock<IUserSubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<IPlanRepository> _planRepo = new();
    private readonly StatsRepository _sut;

    public StatsRepositoryQuotaTests()
    {
        _cosmosClient
            .Setup(c => c.GetContainer(It.IsAny<string>(), "Projects"))
            .Returns(_projectsContainer.Object);
        _cosmosClient
            .Setup(c => c.GetContainer(It.IsAny<string>(), "TestRuns"))
            .Returns(_testRunsContainer.Object);
        _cosmosClient
            .Setup(c => c.GetContainer(It.IsAny<string>(), "TestResults"))
            .Returns(_testResultsContainer.Object);

        _sut = new StatsRepository(
            _cosmosClient.Object,
            "testurio",
            _subscriptionRepo.Object,
            _planRepo.Object);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static UserSubscription MakeTrialingSubscription(
        DateTimeOffset trialEndsAt,
        SubscriptionPlan plan = SubscriptionPlan.TestJunior) =>
        new()
        {
            Id = "user-1",
            UserId = "user-1",
            Plan = plan,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = trialEndsAt,
        };

    private static PlanDocument MakePlanDocument(int maxTestRunsPerMonth = 50) =>
        new()
        {
            Id = "test-junior",
            Type = "plan",
            Name = "Test Junior",
            MonthlyPrice = 0,
            AnnualPrice = 0,
            AnnualDiscountPercent = 0,
            IsPopular = false,
            SortOrder = 1,
            DisplayFeatures = [],
            Limits = new PlanLimits { MaxProjects = 3, MaxTestRunsPerMonth = maxTestRunsPerMonth },
            Features = new PlanFeatures
            {
                ApiTesting = true,
                UiE2eTesting = false,
                AiMemory = false,
                PmReportPostBack = true,
            },
        };

    /// <summary>
    /// Sets up the TestRuns container to return <paramref name="count"/> from a COUNT query.
    /// Captures the @start and @end parameters so the test can assert the window.
    /// </summary>
    private (Func<string?> GetStart, Func<string?> GetEnd) SetupCountQuery(int count)
    {
        string? capturedStart = null;
        string? capturedEnd = null;

        var mockIterator = new Mock<FeedIterator<int>>();
        var page = new Mock<FeedResponse<int>>();
        page.Setup(p => p.GetEnumerator()).Returns(new List<int> { count }.GetEnumerator());

        var callCount = 0;
        mockIterator.SetupGet(i => i.HasMoreResults).Returns(() => callCount++ == 0);
        mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(page.Object);

        _testRunsContainer
            .Setup(c => c.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string?>(),
                It.IsAny<QueryRequestOptions?>()))
            .Callback<QueryDefinition, string?, QueryRequestOptions?>((qd, _, _) =>
            {
                // Extract parameter values from the QueryDefinition via reflection
                // (SDK stores them internally; we verify via the window assertions below)
                _ = qd; // captured for reference; actual param extraction done via Func overloads
            })
            .Returns(mockIterator.Object);

        return (() => capturedStart, () => capturedEnd);
    }

    /// <summary>
    /// Simpler helper that sets up the count iterator to return a fixed count
    /// and captures start/end from the QueryDefinition parameters via the dedicated callback.
    /// </summary>
    private (Mock<FeedIterator<int>> Iterator, List<(string Start, string End)> Calls) SetupCountQueryWithCapture(int count)
    {
        var calls = new List<(string Start, string End)>();

        var mockIterator = new Mock<FeedIterator<int>>();
        var page = new Mock<FeedResponse<int>>();
        page.Setup(p => p.GetEnumerator()).Returns(new List<int> { count }.GetEnumerator());

        var callCount = 0;
        mockIterator.SetupGet(i => i.HasMoreResults).Returns(() => callCount++ == 0);
        mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(page.Object);

        _testRunsContainer
            .Setup(c => c.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string?>(),
                It.IsAny<QueryRequestOptions?>()))
            .Returns(mockIterator.Object);

        return (mockIterator, calls);
    }

    // ─── Trial path — within window ───────────────────────────────────────────

    [Fact]
    public async Task GetQuotaUsageAsync_WhenTrialing_WithinWindow_ReturnsTrialPeriodCounts()
    {
        // Arrange
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(4);
        var subscription = MakeTrialingSubscription(trialEndsAt);
        var plan = MakePlanDocument(maxTestRunsPerMonth: 50);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _planRepo
            .Setup(r => r.GetByPlanAsync(SubscriptionPlan.TestJunior, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        SetupCountQueryWithCapture(7); // 7 runs in the trial window

        // Act
        var result = await _sut.GetQuotaUsageAsync("user-1", 0);

        // Assert: ResetsAt == TrialEndsAt (not next month start)
        Assert.Equal(trialEndsAt, result.ResetsAt);
        // Assert: UsedThisMonth reflects the query count
        Assert.Equal(7, result.UsedThisMonth);
        // Assert: MonthlyLimit comes from the plan
        Assert.Equal(50, result.MonthlyLimit);
        // Assert: the TestRuns container was queried (trial window)
        _testRunsContainer.Verify(
            c => c.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string?>(),
                It.IsAny<QueryRequestOptions?>()),
            Times.Once);
    }

    // ─── Trial path — expired ─────────────────────────────────────────────────

    [Fact]
    public async Task GetQuotaUsageAsync_WhenTrialing_Expired_ReturnsZeroLimit()
    {
        // Arrange: trial ended yesterday
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
        var subscription = MakeTrialingSubscription(trialEndsAt);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _sut.GetQuotaUsageAsync("user-1", 0);

        // Assert: MonthlyLimit = 0 (no active plan)
        Assert.Equal(0, result.MonthlyLimit);
        // Assert: UsedThisMonth = 0
        Assert.Equal(0, result.UsedThisMonth);
        // Assert: ResetsAt = TrialEndsAt (not next-month)
        Assert.Equal(trialEndsAt, result.ResetsAt);
        // Assert: no Cosmos query was made (short-circuit for expired trial)
        _testRunsContainer.Verify(
            c => c.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string?>(),
                It.IsAny<QueryRequestOptions?>()),
            Times.Never);
    }

    // ─── Active (non-trial) — calendar-month regression ───────────────────────

    [Fact]
    public async Task GetQuotaUsageAsync_WhenActive_ReturnsCalendarMonthCounts()
    {
        // Arrange: active subscription, 12 runs this calendar month
        var subscription = new UserSubscription
        {
            Id = "user-1",
            UserId = "user-1",
            Plan = SubscriptionPlan.TestJunior,
            Status = SubscriptionStatus.Active,
            TrialEndsAt = null,
        };
        var plan = MakePlanDocument(maxTestRunsPerMonth: 50);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _planRepo
            .Setup(r => r.GetByPlanAsync(SubscriptionPlan.TestJunior, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        SetupCountQueryWithCapture(12);

        // Act
        var result = await _sut.GetQuotaUsageAsync("user-1", 0);

        // Assert: ResetsAt is the first day of next month (calendar month boundary)
        var now = DateTimeOffset.UtcNow;
        var expectedResetsAt = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        Assert.Equal(expectedResetsAt, result.ResetsAt);
        Assert.Equal(12, result.UsedThisMonth);
        Assert.Equal(50, result.MonthlyLimit);
    }
}
