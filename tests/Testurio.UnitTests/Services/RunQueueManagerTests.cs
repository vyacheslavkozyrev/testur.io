using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Worker.Services;
using Xunit;

namespace Testurio.UnitTests.Services;

public class RunQueueManagerTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
    private readonly Mock<ITestRunJobSender> _jobSender = new();

    private RunQueueManager CreateSut() => new(
        _testRunRepo.Object,
        _runQueueRepo.Object,
        _jobSender.Object,
        NullLogger<RunQueueManager>.Instance);

    [Fact]
    public async Task OnRunCompletedAsync_WhenQueueIsEmpty_DoesNotDispatchJob()
    {
        _runQueueRepo.Setup(r => r.DequeueNextAsync("proj1", default)).ReturnsAsync((QueuedRun?)null);

        var sut = CreateSut();
        await sut.OnRunCompletedAsync("proj1");

        _testRunRepo.VerifyNoOtherCalls();
        _jobSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnRunCompletedAsync_WhenQueueHasItem_CreatesTestRunAndSendsJob()
    {
        var queued = new QueuedRun
        {
            ProjectId = "proj1",
            UserId = "user1",
            JiraIssueKey = "PROJ-2",
            JiraIssueId = "10002"
        };

        _runQueueRepo.Setup(r => r.DequeueNextAsync("proj1", default)).ReturnsAsync(queued);
        _runQueueRepo.Setup(r => r.DeleteAsync("proj1", queued.Id, default)).Returns(Task.CompletedTask);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), default)).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.OnRunCompletedAsync("proj1");

        _runQueueRepo.Verify(r => r.DeleteAsync("proj1", queued.Id, default), Times.Once);
        _testRunRepo.Verify(r => r.CreateAsync(It.Is<TestRun>(t =>
            t.ProjectId == "proj1" &&
            t.JiraIssueKey == "PROJ-2" &&
            t.Status == TestRunStatus.Pending), default), Times.Once);
        _jobSender.Verify(s => s.SendAsync(It.Is<TestRunJobMessage>(m =>
            m.ProjectId == "proj1" && m.JiraIssueId == "10002"), default), Times.Once);
    }

    [Fact]
    public async Task OnRunCompletedAsync_WhenQueueHasItem_DeletesBeforeDispatching()
    {
        var deleteOrder = new List<string>();
        var queued = new QueuedRun
        {
            ProjectId = "proj1",
            UserId = "user1",
            JiraIssueKey = "PROJ-3",
            JiraIssueId = "10003"
        };

        _runQueueRepo.Setup(r => r.DequeueNextAsync("proj1", default)).ReturnsAsync(queued);
        _runQueueRepo.Setup(r => r.DeleteAsync("proj1", queued.Id, default))
            .Callback(() => deleteOrder.Add("delete"))
            .Returns(Task.CompletedTask);
        _testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), default))
            .Callback<TestRun, CancellationToken>((_, _) => deleteOrder.Add("create"))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        _jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), default)).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.OnRunCompletedAsync("proj1");

        Assert.Equal(new[] { "delete", "create" }, deleteOrder);
    }
}
