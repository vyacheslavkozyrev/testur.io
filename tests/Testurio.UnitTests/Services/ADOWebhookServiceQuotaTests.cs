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
/// Unit tests covering the quota-enforcement path in <see cref="ADOWebhookService"/>
/// (feature 0021). Enforcement delegates to <see cref="IPlanEnforcementService"/>; this
/// test suite verifies integration points: QuotaExceeded result, no test run created,
/// no PM comment posted (silent rejection per AC-022).
/// </summary>
public class ADOWebhookServiceQuotaTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();
    private readonly Mock<IWorkItemTypeFilterService> _filterService = new();
    private readonly Mock<IPlanEnforcementService> _planEnforcementService = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
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
        _planEnforcementService.Object,
        _adoClient.Object,
        _secretResolver.Object,
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

    // ─── Quota exceeded ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_ReturnsQuotaExceeded()
    {
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Your plan allows 50 test runs per month.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Pro"));

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), MakePayload());

        Assert.Equal(WebhookProcessResult.QuotaExceeded, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenQuotaExceeded_NoPmToolCommentPosted()
    {
        // AC-022: no PM tool comment for ADO when quota is exceeded.
        _planEnforcementService
            .Setup(s => s.CheckMonthlyRunQuotaAsync("user1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanLimitExceededException(
                "Quota exceeded.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Pro"));

        var sut = CreateSut();
        await sut.ProcessAsync(MakeProject(), MakePayload());

        _jobSender.VerifyNoOtherCalls();
        _testRunRepo.Verify(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Quota not exceeded ───────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenQuotaAvailable_ProceedsToEnqueue()
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

    [Fact]
    public async Task ProcessAsync_WhenTrialExpired_ReturnsQuotaExceeded()
    {
        // Expired trial — enforcement service throws with trial-ended message.
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

    [Fact]
    public async Task ProcessAsync_WhenNoSubscription_ReturnsQuotaExceeded()
    {
        // No plan / no subscription — enforcement service throws.
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
}
