using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Xunit;

// JiraCommentResult is defined in Testurio.Core.Interfaces — used for mock setup.

namespace Testurio.UnitTests.Services;

public class JiraWebhookServiceTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IWorkItemTypeFilterService> _filterService = new();
    private readonly Mock<ILogger<JiraWebhookService>> _logger = new();

    public JiraWebhookServiceTests()
    {
        _secretResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        // Default: allow "Story" only — mirrors behaviour tests were written against.
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), "Story")).Returns(true);
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), It.Is<string>(s => s != "Story"))).Returns(false);

        // [LoggerMessage] checks IsEnabled before calling Log — enable all levels so log calls fire.
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private JiraWebhookService CreateSut() => new(
        _testRunRepo.Object,
        _runQueueRepo.Object,
        _jobSender.Object,
        _jiraApiClient.Object,
        _secretResolver.Object,
        _filterService.Object,
        _logger.Object);

    private static Project MakeProject(string inTestingLabel = "In Testing") => new()
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
        InTestingStatusLabel = inTestingLabel
    };

    private static JsonElement? ToJsonElement(string? value) =>
        value is null ? null : JsonSerializer.Deserialize<JsonElement>($"\"{value}\"");

    private static JiraWebhookPayload MakePayload(
        string issueType = "Story",
        string transitionTo = "In Testing",
        string? description = "A description",
        string? acceptanceCriteria = "Given/when/then")
    {
        return new JiraWebhookPayload
        {
            WebhookEvent = "jira:issue_updated",
            Issue = new JiraIssue
            {
                Id = "10001",
                Key = "PROJ-1",
                Fields = new JiraIssueFields
                {
                    IssueType = new JiraIssueType { Name = issueType },
                    Status = new JiraStatus { Name = transitionTo },
                    Description = description,
                    AcceptanceCriteria = ToJsonElement(acceptanceCriteria)
                }
            },
            Changelog = new JiraChangelog
            {
                Items = [new JiraChangelogItem { Field = "status", ToString = transitionTo }]
            }
        };
    }

    [Fact]
    public async Task ProcessAsync_WhenEventIsNotTransitioned_ReturnsIgnored()
    {
        var payload = new JiraWebhookPayload { WebhookEvent = "jira:issue_created", Issue = MakePayload().Issue };
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Ignored, result);
        _testRunRepo.VerifyNoOtherCalls();
        _runQueueRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenIssueTypeIsNotStory_ReturnsIgnored()
    {
        var payload = MakePayload(issueType: "Bug");
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Ignored, result);
        _testRunRepo.VerifyNoOtherCalls();
        _runQueueRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenStatusDoesNotMatchConfiguredLabel_ReturnsIgnored()
    {
        var payload = MakePayload(transitionTo: "In Testing");
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject("In Review"), payload);

        Assert.Equal(WebhookProcessResult.Ignored, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenNoActiveRun_EnqueuesJobAndReturnsEnqueued()
    {
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync((TestRun?)null);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), default)).Returns(Task.CompletedTask);

        var payload = MakePayload();
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Enqueued, result);
        _jobSender.Verify(s => s.SendAsync(It.Is<TestRunJobMessage>(m => m.ProjectId == "proj1"), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenActiveRunExists_AddsToQueueAndReturnsQueued()
    {
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync(new TestRun
        {
            ProjectId = "proj1", UserId = "user1", JiraIssueKey = "PROJ-0", JiraIssueId = "10000", Status = TestRunStatus.Active
        });
        _runQueueRepo.Setup(r => r.ExistsAsync("proj1", "10001", default)).ReturnsAsync(false);
        _runQueueRepo.Setup(r => r.EnqueueAsync(It.IsAny<QueuedRun>(), default))
            .ReturnsAsync((QueuedRun q, CancellationToken _) => q);

        var payload = MakePayload();
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Queued, result);
        _runQueueRepo.Verify(r => r.EnqueueAsync(It.Is<QueuedRun>(q => q.JiraIssueId == "10001"), default), Times.Once);
        _jobSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenDuplicateInQueue_ReturnsQueuedWithoutAddingDuplicate()
    {
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync(new TestRun
        {
            ProjectId = "proj1", UserId = "user1", JiraIssueKey = "PROJ-0", JiraIssueId = "10000", Status = TestRunStatus.Active
        });
        _runQueueRepo.Setup(r => r.ExistsAsync("proj1", "10001", default)).ReturnsAsync(true);

        var payload = MakePayload();
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Queued, result);
        _runQueueRepo.Verify(r => r.EnqueueAsync(It.IsAny<QueuedRun>(), default), Times.Never);
        _jobSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenDescriptionMissing_SkipsAndPostsComment()
    {
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jiraApiClient.Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(JiraCommentResult.Success());

        var payload = MakePayload(description: null);
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Skipped, result);
        _testRunRepo.Verify(r => r.CreateAsync(It.Is<TestRun>(t => t.Status == TestRunStatus.Skipped), default), Times.Once);
        _jiraApiClient.Verify(c => c.PostCommentAsync(It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(s => s.Contains("description")), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenAcceptanceCriteriaFieldNull_StillEnqueues()
    {
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync((TestRun?)null);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), default)).Returns(Task.CompletedTask);

        var payload = MakePayload(acceptanceCriteria: null);
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Enqueued, result);
        _jobSender.Verify(s => s.SendAsync(It.Is<TestRunJobMessage>(m => m.ProjectId == "proj1"), default), Times.Once);
    }

    // ─── Work item type filter behaviour (AC-012–016) ────────────────────────

    [Fact]
    public async Task ProcessAsync_WhenIssueTypeMatchesAllowedType_Enqueues()
    {
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), "Story")).Returns(true);
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync((TestRun?)null);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), default)).Returns(Task.CompletedTask);

        var payload = MakePayload(issueType: "Story");
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        Assert.Equal(WebhookProcessResult.Enqueued, result);
        _jobSender.Verify(s => s.SendAsync(It.Is<TestRunJobMessage>(m => m.ProjectId == "proj1"), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenIssueTypeNotInAllowedList_DropsEventSilently()
    {
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), "Task")).Returns(false);

        var payload = MakePayload(issueType: "Task");
        var sut = CreateSut();

        var result = await sut.ProcessAsync(MakeProject(), payload);

        // AC-014: silently dropped — no test run created, no comment posted, no run enqueued
        Assert.Equal(WebhookProcessResult.Ignored, result);
        _testRunRepo.VerifyNoOtherCalls();
        _jiraApiClient.VerifyNoOtherCalls();
        _runQueueRepo.Verify(r => r.EnqueueAsync(It.IsAny<QueuedRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenEventIsFiltered_WritesStructuredLogEntry()
    {
        // AC-015: a structured log entry must be written for every dropped event
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), "Task")).Returns(false);

        var payload = MakePayload(issueType: "Task");
        var sut = CreateSut();

        await sut.ProcessAsync(MakeProject(), payload);

        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("webhook_filtered") &&
                    state.ToString()!.Contains("issue_type_not_allowed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenIssueTypeFieldMissing_PassesEmptyStringToFilterService()
    {
        // Missing issue type field (null) → empty string → filter service decides
        _filterService.Setup(f => f.IsAllowed(It.IsAny<Project>(), string.Empty)).Returns(false);

        var payload = new JiraWebhookPayload
        {
            WebhookEvent = "jira:issue_updated",
            Issue = new JiraIssue
            {
                Id = "10001",
                Key = "PROJ-1",
                Fields = new JiraIssueFields
                {
                    IssueType = null, // missing
                    Status = new JiraStatus { Name = "In Testing" },
                    Description = "A description"
                }
            },
            Changelog = new JiraChangelog
            {
                Items = [new JiraChangelogItem { Field = "status", ToString = "In Testing" }]
            }
        };

        var sut = CreateSut();
        var result = await sut.ProcessAsync(MakeProject(), payload);

        // Filter service is called with empty string — the decision is its responsibility (AC-016)
        _filterService.Verify(f => f.IsAllowed(It.IsAny<Project>(), string.Empty), Times.Once);
        Assert.Equal(WebhookProcessResult.Ignored, result);
    }
}
