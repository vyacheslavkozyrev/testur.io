using System.Text.Json;
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
/// Unit tests covering the quota-enforcement path added to <see cref="JiraWebhookService"/>
/// in feature 0021.
/// </summary>
public class JiraWebhookServiceQuotaTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IWorkItemTypeFilterService> _filterService = new();
    private readonly Mock<IQuotaPolicy> _quotaPolicy = new();
    private readonly Mock<IUserSubscriptionRepository> _subscriptionRepository = new();
    private readonly Mock<ILogger<JiraWebhookService>> _logger = new();

    public JiraWebhookServiceQuotaTests()
    {
        _secretResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        // Allow "Story" issue type by default.
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), "Story")).Returns(true);
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), It.Is<string>(s => s != "Story"))).Returns(false);

        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _jiraApiClient
            .Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());
    }

    private JiraWebhookService CreateSut() => new(
        _testRunRepo.Object,
        _runQueueRepo.Object,
        _jobSender.Object,
        _jiraApiClient.Object,
        _secretResolver.Object,
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
        TestingStrategy = "API and UI tests",
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "token",
        JiraWebhookSecretRef = "secret",
        InTestingStatusLabel = "In Testing"
    };

    private static JiraWebhookPayload MakePayload(string issueType = "Story") =>
        new()
        {
            WebhookEvent = "jira:issue_updated",
            Issue = new JiraIssue
            {
                Id = "10001",
                Key = "PROJ-1",
                Fields = new JiraIssueFields
                {
                    IssueType = new JiraIssueType { Name = issueType },
                    Status = new JiraStatus { Name = "In Testing" },
                    Description = "A description",
                    AcceptanceCriteria = JsonSerializer.Deserialize<JsonElement>("\"Some criteria\"")
                }
            },
            Changelog = new JiraChangelog
            {
                Items = [new JiraChangelogItem { Field = "status", ToString = "In Testing" }]
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
        SetupActiveSubscription(SubscriptionPlan.TestJunior, 10);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10); // usedToday == dailyLimit

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_PostsQuotaExhaustedCommentToJira()
    {
        SetupActiveSubscription(SubscriptionPlan.TestJunior, 10);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        _jiraApiClient.Verify(c => c.PostCommentAsync(
            It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(s => s.Contains("quota") && s.Contains("10")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_NoTestRunCreated()
    {
        SetupActiveSubscription(SubscriptionPlan.TestJunior, 10);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        _testRunRepo.Verify(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobSender.VerifyNoOtherCalls();
    }

    // ─── No active subscription ───────────────────────────────────────────────

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
    public async Task ProcessAsync_WhenNoSubscription_PostsNoSubscriptionCommentToJira()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSubscription?)null);

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        _jiraApiClient.Verify(c => c.PostCommentAsync(
            It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(s => s.Contains("subscription")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenSubscriptionIsExpired_ReturnsQuotaExceeded()
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

    [Fact]
    public async Task ProcessAsync_WhenSubscriptionIsNone_ReturnsQuotaExceeded()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user1",
                UserId = "user1",
                Plan = SubscriptionPlan.TestPro,
                Status = SubscriptionStatus.None
            });

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    // ─── Quota not exceeded ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaNotExceeded_ProceedsToEnqueue()
    {
        SetupActiveSubscription(SubscriptionPlan.TestPro, 30);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // well below 30

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

    // ─── Trialing status uses plan-tier limit ─────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenTrialingStatus_UsesPlanTierLimit()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user1",
                UserId = "user1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing
            });
        _quotaPolicy.Setup(p => p.GetDailyLimit(SubscriptionPlan.TestJunior)).Returns(10);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(9); // one below limit

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

        // Trialing user with 9/10 runs used should still be allowed through.
        Assert.Equal(WebhookProcessResult.Enqueued, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenTrialingStatusAndAtLimit_ReturnsQuotaExceeded()
    {
        _subscriptionRepository
            .Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSubscription
            {
                Id = "user1",
                UserId = "user1",
                Plan = SubscriptionPlan.TestJunior,
                Status = SubscriptionStatus.Trialing
            });
        _quotaPolicy.Setup(p => p.GetDailyLimit(SubscriptionPlan.TestJunior)).Returns(10);
        _testRunRepo
            .Setup(r => r.CountTodayAsync("user1", It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10); // exactly at limit

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }
}
