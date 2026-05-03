using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Xunit;

namespace Testurio.UnitTests.Services;

public class JiraWebhookServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();

    private JiraWebhookService CreateSut() => new(
        _projectRepo.Object,
        _testRunRepo.Object,
        _runQueueRepo.Object,
        _jobSender.Object,
        _jiraApiClient.Object,
        NullLogger<JiraWebhookService>.Instance);

    private static Project MakeProject(string inTestingLabel = "In Testing") => new()
    {
        Id = "proj1",
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "token",
        JiraWebhookSecretRef = "secret",
        InTestingStatusLabel = inTestingLabel
    };

    private static JiraWebhookPayload MakePayload(
        string issueType = "Story",
        string transitionTo = "In Testing",
        string? description = "A description",
        string? acceptanceCriteria = "Given/when/then")
    {
        return new JiraWebhookPayload
        {
            WebhookEvent = "jira:issue_transitioned",
            Issue = new JiraIssue
            {
                Id = "10001",
                Key = "PROJ-1",
                Fields = new JiraIssueFields
                {
                    IssueType = new JiraIssueType { Name = issueType },
                    Status = new JiraStatus { Name = transitionTo },
                    Description = description,
                    AcceptanceCriteria = acceptanceCriteria
                }
            },
            Transition = new JiraTransition { To = new JiraTransitionTo { Name = transitionTo } }
        };
    }

    [Fact]
    public async Task ProcessAsync_WhenEventIsNotTransitioned_ReturnsIgnored()
    {
        var payload = new JiraWebhookPayload { WebhookEvent = "jira:issue_updated", Issue = MakePayload().Issue, Transition = MakePayload().Transition };
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Ignored, result);
        _projectRepo.VerifyNoOtherCalls();
        _testRunRepo.VerifyNoOtherCalls();
        _runQueueRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenIssueTypeIsNotStory_ReturnsIgnored()
    {
        var payload = MakePayload(issueType: "Bug");
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Ignored, result);
        _projectRepo.VerifyNoOtherCalls();
        _testRunRepo.VerifyNoOtherCalls();
        _runQueueRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenStatusDoesNotMatchConfiguredLabel_ReturnsIgnored()
    {
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default))
            .ReturnsAsync(MakeProject("In Review"));

        var payload = MakePayload(transitionTo: "In Testing");
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Ignored, result);
    }

    [Fact]
    public async Task ProcessAsync_WhenNoActiveRun_EnqueuesJobAndReturnsEnqueued()
    {
        var project = MakeProject();
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default)).ReturnsAsync(project);
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync((TestRun?)null);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), default)).Returns(Task.CompletedTask);

        var payload = MakePayload();
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Enqueued, result);
        _jobSender.Verify(s => s.SendAsync(It.Is<TestRunJobMessage>(m => m.ProjectId == "proj1"), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenActiveRunExists_AddsToQueueAndReturnsEnqueued()
    {
        var project = MakeProject();
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default)).ReturnsAsync(project);
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync(new TestRun
        {
            ProjectId = "proj1", UserId = "user1", JiraIssueKey = "PROJ-0", JiraIssueId = "10000", Status = TestRunStatus.Active
        });
        _runQueueRepo.Setup(r => r.ExistsAsync("proj1", "10001", default)).ReturnsAsync(false);
        _runQueueRepo.Setup(r => r.EnqueueAsync(It.IsAny<QueuedRun>(), default))
            .ReturnsAsync((QueuedRun q, CancellationToken _) => q);

        var payload = MakePayload();
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Queued, result);
        _runQueueRepo.Verify(r => r.EnqueueAsync(It.Is<QueuedRun>(q => q.JiraIssueId == "10001"), default), Times.Once);
        _jobSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenDuplicateInQueue_ReturnsEnqueuedWithoutAddingDuplicate()
    {
        var project = MakeProject();
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default)).ReturnsAsync(project);
        _testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", default)).ReturnsAsync(new TestRun
        {
            ProjectId = "proj1", UserId = "user1", JiraIssueKey = "PROJ-0", JiraIssueId = "10000", Status = TestRunStatus.Active
        });
        _runQueueRepo.Setup(r => r.ExistsAsync("proj1", "10001", default)).ReturnsAsync(true);

        var payload = MakePayload();
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Queued, result);
        _runQueueRepo.Verify(r => r.EnqueueAsync(It.IsAny<QueuedRun>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenDescriptionMissing_SkipsAndPostsComment()
    {
        var project = MakeProject();
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default)).ReturnsAsync(project);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jiraApiClient.Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        var payload = MakePayload(description: null);
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Skipped, result);
        _testRunRepo.Verify(r => r.CreateAsync(It.Is<TestRun>(t => t.Status == TestRunStatus.Skipped), default), Times.Once);
        _jiraApiClient.Verify(c => c.PostCommentAsync(It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(s => s.Contains("description")), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenAcceptanceCriteriaMissing_SkipsAndPostsComment()
    {
        var project = MakeProject();
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default)).ReturnsAsync(project);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jiraApiClient.Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        var payload = MakePayload(acceptanceCriteria: null);
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Skipped, result);
        _jiraApiClient.Verify(c => c.PostCommentAsync(It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(s => s.Contains("acceptance criteria")), default), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenBothMissing_SkipsAndPostsCommentMentioningBoth()
    {
        var project = MakeProject();
        _projectRepo.Setup(r => r.GetByIdAsync("user1", "proj1", default)).ReturnsAsync(project);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jiraApiClient.Setup(c => c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        var payload = MakePayload(description: null, acceptanceCriteria: null);
        var sut = CreateSut();

        var result = await sut.ProcessAsync("user1", "proj1", payload);

        Assert.Equal(WebhookProcessResult.Skipped, result);
        _jiraApiClient.Verify(c => c.PostCommentAsync(It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(s => s.Contains("description and acceptance criteria")), default), Times.Once);
    }
}
