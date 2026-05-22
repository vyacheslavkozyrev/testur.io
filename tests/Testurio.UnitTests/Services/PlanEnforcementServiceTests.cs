using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Enforcement;

namespace Testurio.UnitTests.Services;

public class PlanEnforcementServiceTests
{
    private readonly Mock<IUserSubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<IPlanRepository> _planRepo = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly PlanEnforcementService _sut;

    public PlanEnforcementServiceTests()
    {
        _sut = new PlanEnforcementService(
            _subscriptionRepo.Object,
            _planRepo.Object,
            _projectRepo.Object,
            _testRunRepo.Object);
    }

    // ─── Shared helpers ───────────────────────────────────────────────────────

    private static UserSubscription MakeSubscription(
        SubscriptionPlan plan = SubscriptionPlan.TestJunior,
        SubscriptionStatus status = SubscriptionStatus.Active) =>
        new()
        {
            Id = "user-1",
            UserId = "user-1",
            Plan = plan,
            Status = status,
        };

    private static PlanDocument MakePlanDocument(
        string id = "test-junior",
        int maxProjects = 3,
        int maxTestRunsPerMonth = 50,
        bool apiTesting = true,
        bool uiE2eTesting = false,
        bool aiMemory = false,
        bool pmReportPostBack = true) =>
        new()
        {
            Id = id,
            Type = "plan",
            Name = id,
            MonthlyPrice = 0,
            AnnualPrice = 0,
            AnnualDiscountPercent = 0,
            IsPopular = false,
            SortOrder = 1,
            DisplayFeatures = [],
            Limits = new PlanLimits { MaxProjects = maxProjects, MaxTestRunsPerMonth = maxTestRunsPerMonth },
            Features = new PlanFeatures
            {
                ApiTesting = apiTesting,
                UiE2eTesting = uiE2eTesting,
                AiMemory = aiMemory,
                PmReportPostBack = pmReportPostBack,
            },
        };

    // ─── GetEffectivePlanAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetEffectivePlanAsync_ReturnsNull_WhenNoSubscription()
    {
        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        var result = await _sut.GetEffectivePlanAsync("user-1");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(SubscriptionStatus.None)]
    [InlineData(SubscriptionStatus.Expired)]
    [InlineData(SubscriptionStatus.PaymentFailed)]
    public async Task GetEffectivePlanAsync_ReturnsNull_WhenSubscriptionStatusIsInactive(SubscriptionStatus status)
    {
        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSubscription(status: status));

        var result = await _sut.GetEffectivePlanAsync("user-1");

        Assert.Null(result);
        _planRepo.Verify(r => r.GetByPlanAsync(It.IsAny<SubscriptionPlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.Trialing)]
    [InlineData(SubscriptionStatus.CancelledPendingExpiry)]
    public async Task GetEffectivePlanAsync_ReturnsPlanDocument_WhenSubscriptionIsActive(SubscriptionStatus status)
    {
        var plan = MakePlanDocument();
        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSubscription(status: status));
        _planRepo
            .Setup(r => r.GetByPlanAsync(SubscriptionPlan.TestJunior, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _sut.GetEffectivePlanAsync("user-1");

        Assert.NotNull(result);
        Assert.Equal("test-junior", result.Id);
    }

    // ─── CheckProjectLimitAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CheckProjectLimitAsync_Throws_WhenNoPlan()
    {
        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckProjectLimitAsync("user-1"));

        Assert.Equal("maxProjects", ex.LimitName);
        Assert.Equal("Test Junior", ex.RequiredPlan);
    }

    [Fact]
    public async Task CheckProjectLimitAsync_Throws_WhenAtProjectLimit()
    {
        var plan = MakePlanDocument(maxProjects: 3);
        SetupActivePlan(plan);
        _projectRepo
            .Setup(r => r.ListByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 3).Select(_ => new Core.Entities.Project { UserId = "user-1", Name = "P", ProductUrl = "https://p.example.com", TestingStrategy = "API" }).ToArray());

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckProjectLimitAsync("user-1"));

        Assert.Equal("maxProjects", ex.LimitName);
        Assert.Equal("Test Pro", ex.RequiredPlan); // next tier from test-junior
    }

    [Fact]
    public async Task CheckProjectLimitAsync_DoesNotThrow_WhenBelowProjectLimit()
    {
        var plan = MakePlanDocument(maxProjects: 3);
        SetupActivePlan(plan);
        _projectRepo
            .Setup(r => r.ListByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 2).Select(_ => new Core.Entities.Project { UserId = "user-1", Name = "P", ProductUrl = "https://p.example.com", TestingStrategy = "API" }).ToArray());

        // Should not throw
        await _sut.CheckProjectLimitAsync("user-1");
    }

    [Fact]
    public async Task CheckProjectLimitAsync_DoesNotThrow_WhenMaxProjectsIsUnlimited()
    {
        var plan = MakePlanDocument(id: "team", maxProjects: -1);
        SetupActivePlan(plan, SubscriptionPlan.Team);
        // Even with many projects, no exception expected
        _projectRepo
            .Setup(r => r.ListByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 100).Select(_ => new Core.Entities.Project { UserId = "user-1", Name = "P", ProductUrl = "https://p.example.com", TestingStrategy = "API" }).ToArray());

        await _sut.CheckProjectLimitAsync("user-1");

        _projectRepo.Verify(r => r.ListByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── CheckMonthlyRunQuotaAsync ────────────────────────────────────────────

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_Throws_WhenNoPlan()
    {
        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckMonthlyRunQuotaAsync("user-1"));

        Assert.Equal("maxTestRunsPerMonth", ex.LimitName);
        Assert.Equal("Test Junior", ex.RequiredPlan);
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_Throws_WhenAtMonthlyRunLimit()
    {
        var plan = MakePlanDocument(maxTestRunsPerMonth: 50);
        SetupActivePlan(plan);
        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                "user-1",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckMonthlyRunQuotaAsync("user-1"));

        Assert.Equal("maxTestRunsPerMonth", ex.LimitName);
        Assert.Equal("Test Pro", ex.RequiredPlan); // next tier from test-junior
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_DoesNotThrow_WhenBelowMonthlyRunLimit()
    {
        var plan = MakePlanDocument(maxTestRunsPerMonth: 50);
        SetupActivePlan(plan);
        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                "user-1",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(49);

        await _sut.CheckMonthlyRunQuotaAsync("user-1");
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_DoesNotThrow_WhenMaxRunsIsUnlimited()
    {
        var plan = MakePlanDocument(id: "test-pro", maxTestRunsPerMonth: -1);
        SetupActivePlan(plan, SubscriptionPlan.TestPro);

        await _sut.CheckMonthlyRunQuotaAsync("user-1");

        _testRunRepo.Verify(
            r => r.CountByUserForMonthAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_UsesCurrentMonthWindow()
    {
        var plan = MakePlanDocument(maxTestRunsPerMonth: 50);
        SetupActivePlan(plan);

        DateTimeOffset capturedStart = default;
        DateTimeOffset capturedEnd = default;

        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, CancellationToken>((_, s, e, _) =>
            {
                capturedStart = s;
                capturedEnd = e;
            })
            .ReturnsAsync(0);

        await _sut.CheckMonthlyRunQuotaAsync("user-1");

        var now = DateTimeOffset.UtcNow;
        var expectedStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedEnd = expectedStart.AddMonths(1);

        Assert.Equal(expectedStart, capturedStart);
        Assert.Equal(expectedEnd, capturedEnd);
    }

    // ─── CheckMonthlyRunQuotaAsync — trial paths ──────────────────────────────

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_WhenTrialing_WithinWindow_CountsRunsInTrialPeriod()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(3);
        var expectedStart = trialEndsAt.AddDays(-14);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });
        _planRepo
            .Setup(r => r.GetByPlanAsync(SubscriptionPlan.TestJunior, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePlanDocument(maxTestRunsPerMonth: 50));

        DateTimeOffset capturedStart = default;
        DateTimeOffset capturedEnd = default;
        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                "user-1",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, CancellationToken>((_, s, e, _) =>
            {
                capturedStart = s;
                capturedEnd = e;
            })
            .ReturnsAsync(0);

        await _sut.CheckMonthlyRunQuotaAsync("user-1");

        // Verify the repo was called with the trial-period window.
        Assert.Equal(expectedStart, capturedStart);
        Assert.Equal(trialEndsAt, capturedEnd);
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_WhenTrialing_WithinWindow_UnderLimit_DoesNotThrow()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(5);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });
        _planRepo
            .Setup(r => r.GetByPlanAsync(SubscriptionPlan.TestJunior, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePlanDocument(maxTestRunsPerMonth: 50));
        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                "user-1",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(49);

        // Should not throw
        await _sut.CheckMonthlyRunQuotaAsync("user-1");
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_WhenTrialing_WithinWindow_AtLimit_Throws()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(2);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });
        _planRepo
            .Setup(r => r.GetByPlanAsync(SubscriptionPlan.TestJunior, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePlanDocument(maxTestRunsPerMonth: 50));
        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                "user-1",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckMonthlyRunQuotaAsync("user-1"));

        Assert.Equal("maxTestRunsPerMonth", ex.LimitName);
        Assert.Equal("Test Junior", ex.RequiredPlan);
        // Message contains trial end date (AC-004).
        Assert.Contains(trialEndsAt.ToString("yyyy-MM-dd"), ex.Message);
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_WhenTrialing_TrialExpired_Throws()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(-1); // expired

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckMonthlyRunQuotaAsync("user-1"));

        Assert.Equal("maxTestRunsPerMonth", ex.LimitName);
        Assert.Equal("Test Junior", ex.RequiredPlan);
        Assert.Contains("trial has ended", ex.Message);
    }

    [Fact]
    public async Task CheckMonthlyRunQuotaAsync_WhenActive_UsesCalendarMonth()
    {
        // Regression: non-trial path still uses a calendar-month window.
        var plan = MakePlanDocument(maxTestRunsPerMonth: 50);
        SetupActivePlan(plan);

        DateTimeOffset capturedStart = default;
        DateTimeOffset capturedEnd = default;
        _testRunRepo
            .Setup(r => r.CountByUserForMonthAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, CancellationToken>((_, s, e, _) =>
            {
                capturedStart = s;
                capturedEnd = e;
            })
            .ReturnsAsync(0);

        await _sut.CheckMonthlyRunQuotaAsync("user-1");

        var now = DateTimeOffset.UtcNow;
        var expectedStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedEnd = expectedStart.AddMonths(1);

        Assert.Equal(expectedStart, capturedStart);
        Assert.Equal(expectedEnd, capturedEnd);
    }

    // ─── CheckProjectLimitAsync — trial paths ─────────────────────────────────

    [Fact]
    public async Task CheckProjectLimitAsync_WhenTrialing_Below2Projects_Allows()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(7);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });
        _projectRepo
            .Setup(r => r.ListByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Enumerable.Range(0, 1)
                    .Select(_ => new Core.Entities.Project { UserId = "user-1", Name = "P", ProductUrl = "https://p.example.com", TestingStrategy = "API" })
                    .ToArray());

        // Should not throw
        await _sut.CheckProjectLimitAsync("user-1");
    }

    [Fact]
    public async Task CheckProjectLimitAsync_WhenTrialing_At2Projects_Throws()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(7);

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });
        _projectRepo
            .Setup(r => r.ListByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Enumerable.Range(0, 2)
                    .Select(_ => new Core.Entities.Project { UserId = "user-1", Name = "P", ProductUrl = "https://p.example.com", TestingStrategy = "API" })
                    .ToArray());

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckProjectLimitAsync("user-1"));

        Assert.Equal("maxProjects", ex.LimitName);
        Assert.Equal("Test Junior", ex.RequiredPlan);
        Assert.Contains("maximum of 2 projects", ex.Message);
    }

    [Fact]
    public async Task CheckProjectLimitAsync_WhenTrialing_Expired_Throws()
    {
        var trialEndsAt = DateTimeOffset.UtcNow.AddDays(-2); // expired

        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user-1",
                UserId = "user-1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = trialEndsAt,
            });

        var ex = await Assert.ThrowsAsync<PlanLimitExceededException>(
            () => _sut.CheckProjectLimitAsync("user-1"));

        Assert.Equal("maxProjects", ex.LimitName);
        Assert.Equal("Test Junior", ex.RequiredPlan);
        Assert.Contains("trial has ended", ex.Message);
    }

    [Fact]
    public async Task CheckProjectLimitAsync_WhenActive_UsesPlanMaxProjects()
    {
        // Regression: non-trial path uses plan.Limits.MaxProjects.
        var plan = MakePlanDocument(maxProjects: 5);
        SetupActivePlan(plan);
        _projectRepo
            .Setup(r => r.ListByUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Enumerable.Range(0, 4)
                    .Select(_ => new Core.Entities.Project { UserId = "user-1", Name = "P", ProductUrl = "https://p.example.com", TestingStrategy = "API" })
                    .ToArray());

        // Should not throw (4 < 5)
        await _sut.CheckProjectLimitAsync("user-1");

        // No PlanLimitExceededException when below the plan limit.
        _subscriptionRepo.Verify(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void SetupActivePlan(PlanDocument plan, SubscriptionPlan planEnum = SubscriptionPlan.TestJunior)
    {
        _subscriptionRepo
            .Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSubscription(plan: planEnum, status: SubscriptionStatus.Active));
        _planRepo
            .Setup(r => r.GetByPlanAsync(planEnum, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
    }
}
