using Microsoft.Extensions.Logging;
using Moq;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Xunit;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Unit tests covering the quota-enforcement path added to <see cref="ADOWebhookService"/>
/// in feature 0021. Per AC-022, no PM tool comment is posted for ADO — the rejection is silent.
/// </summary>
public class ADOWebhookServiceQuotaTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();
    private readonly Mock<IWorkItemTypeFilterService> _filterService = new();
    private readonly Mock<IQuotaPolicy> _quotaPolicy = new();
    private readonly Mock<IUserSubscriptionRepository> _subscriptionRepository = new();
    private readonly Mock<ILogger<ADOWebhookService>> _logger = new();

    public ADOWebhookServiceQuotaTests()
    {
        // Default: allow "User Story" issue type.
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), "User Story")).Returns(true);
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), It.Is<string>(s => s != "User Story"))).Returns(false);

        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private ADOWebhookService CreateSut() => new(
        _testRunRepo.Object,
        _runQueueRepo.Object,
        _jobSender.Object,
        _filterService.Object,
        _quotaPolicy.Object,
        _subscriptionRepository.Object,
        _logger.Object);

    private static Project MakeProject() => new()
    {
        Id = "proj1",
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API tests",
        AdoInTestingStatus = "In Testing"
    };

    private static ADOWebhookPayload MakePayload(string workItemType = "User Story") =>
        new()
        {
            EventType = "workitem.updated",
            Resource = new ADOWebhookResource
            {
                WorkItemId = 42,
                Fields = new ADOFieldChanges
                {
                    State = new ADOFieldChange { NewValue = "In Testing" }
                },
                Revision = new ADORevision
                {
                    Fields = new ADORevisionFields { WorkItemType = workItemType }
                }
            }
        };

    private void SetupActiveSubscription(SubscriptionPlan plan, int dailyLimit)
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user1",
                UserId = "user1",
                Plan = plan,
                Status = SubscriptionStatus.Active
            });
        _quotaPolicy.Setup(p => p.GetDailyLimit(plan)).Returns(dailyLimit);
    }

    // ─── Quota exceeded ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_ReturnsQuotaExceeded()
    {
        SetupActiveSubscription(SubscriptionPlan.TestPro, 30);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30); // usedToday == dailyLimit

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_NoPmToolCommentPosted()
    {
        // AC-022: no PM tool comment for ADO when quota is exceeded.
        SetupActiveSubscription(SubscriptionPlan.TestPro, 30);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        // No ADO client or Jira client is injected — verify no other interactions with queue/job
        _jobSender.VerifyNoOtherCalls();
        _testRunRepo.Verify(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenNoSubscription_ReturnsQuotaExceeded()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenExpiredSubscription_ReturnsQuotaExceeded()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user1",
                UserId = "user1",
                Plan = SubscriptionPlan.TestPro,
                Status = SubscriptionStatus.Expired
            });

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    // ─── Quota not exceeded ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaAvailable_ProceedsToEnqueue()
    {
        SetupActiveSubscription(SubscriptionPlan.Team, 100);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10); // well below 100

        _testRunRepo
            .Setup(r => r.GetActiveRunAsync("proj1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun?)null);
        _testRunRepo
            .Setup(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender
            .Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.Enqueued, result);
        _jobSender.Verify(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
