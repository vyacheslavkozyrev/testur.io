using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Xunit;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Unit tests covering the quota-enforcement path in <see cref="JiraWebhookService"/>
/// (feature 0021). Enforcement delegates to <see cref="IPlanEnforcementService"/>; this
/// test suite verifies integration points: QuotaExceeded result, Jira comment posted,
/// and no test run created when quota is exceeded.
/// </summary>
public class JiraWebhookServiceQuotaTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IWorkItemTypeFilterService> _filterService = new();
    private readonly Mock<IPlanEnforcementService> _planEnforcementService = new();
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
        _planEnforcementService.Object,
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

    // ─── Quota exceeded ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_ReturnsQuotaExceeded()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your plan allows 50 test runs per month. Your quota resets on 2026-06-01. Upgrade to Test Pro to run more tests.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Pro"));

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_PostsCommentToJira()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your plan allows 50 test runs per month.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Pro"));

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        _jiraApiClient.Verify(c => c.PostCommentAsync(
            It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_NoTestRunCreated()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Quota exceeded.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Pro"));

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        _testRunRepo.Verify(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobSender.VerifyNoOtherCalls();
    }

    // ─── No active subscription / expired ────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenNoSubscription_ReturnsQuotaExceeded()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your plan allows 0 test runs per month.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Junior"));

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenSubscriptionIsExpired_ReturnsQuotaExceeded()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your plan allows 0 test runs per month.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Junior"));

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    // ─── Quota not exceeded ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaNotExceeded_ProceedsToEnqueue()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask); // no exception = quota OK

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

    // ─── Trial paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenTrialingWithinWindow_BelowLimit_ProceedsToEnqueue()
    {
        // Trial enforcement succeeds (no exception).
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
    }

    [Fact]
    public async Task ProcessAsync_WhenTrialingAtLimit_ReturnsQuotaExceeded()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your trial allows 50 test runs. Trial ends on 2026-06-03.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Junior"));

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenTrialExpired_ReturnsQuotaExceeded()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your trial has ended. Purchase a plan to run more tests.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Junior"));

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }
}
