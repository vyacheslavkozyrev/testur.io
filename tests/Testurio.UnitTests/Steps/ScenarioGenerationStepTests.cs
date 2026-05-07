using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Plugins.StoryParserPlugin;
using Testurio.Plugins.TestGeneratorPlugin;
using Testurio.Worker.Steps;
using Xunit;

namespace Testurio.UnitTests.Steps;

public class ScenarioGenerationStepTests
{
    private readonly Mock<IJiraStoryClient> _jiraStoryClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IChatCompletionService> _chatCompletion = new();
    private readonly Mock<ITestScenarioRepository> _scenarioRepository = new();
    private readonly Mock<ITestRunRepository> _testRunRepository = new();

    private ScenarioGenerationStep CreateSut() => new(
        _jiraStoryClient.Object,
        _secretResolver.Object,
        new StoryParserPlugin(),
        new TestGeneratorPlugin(_chatCompletion.Object, NullLogger<TestGeneratorPlugin>.Instance),
        _scenarioRepository.Object,
        _testRunRepository.Object,
        NullLogger<ScenarioGenerationStep>.Instance);

    private static TestRun MakeRun() => new()
    {
        Id = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = TestRunStatus.Active
    };

    private static Project MakeProject() => new()
    {
        Id = "proj1",
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "token-ref",
        JiraWebhookSecretRef = "secret-ref",
        InTestingStatusLabel = "In Testing"
    };

    private void SetupSecretResolver() =>
        _secretResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("resolved-token");

    private void SetupJiraStoryContent(string description = "Story description", string ac = "AC-001: criteria") =>
        _jiraStoryClient
            .Setup(c => c.GetStoryContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraStoryContent { Description = description, AcceptanceCriteria = ac });

    private void SetupChatResponse(string json)
    {
        var msg = new ChatMessageContent(AuthorRole.Assistant, json);
        _chatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([msg]);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsScenariosAndPersists()
    {
        SetupSecretResolver();
        SetupJiraStoryContent();
        SetupChatResponse("""
            [{"title":"Test","steps":[{"order":1,"description":"Do it","expectedResult":"OK"}]}]
            """);
        _scenarioRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<TestScenario>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.ExecuteAsync(MakeRun(), MakeProject());

        Assert.Single(result);
        Assert.Equal("Test", result[0].Title);
        _scenarioRepository.Verify(r => r.CreateBatchAsync(It.Is<IEnumerable<TestScenario>>(s => s.Count() == 1), It.IsAny<CancellationToken>()), Times.Once);
        _testRunRepository.Verify(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_JiraFetchFails_FailsRunAndThrows()
    {
        SetupSecretResolver();
        _jiraStoryClient
            .Setup(c => c.GetStoryContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JiraStoryContent?)null);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<ScenarioGenerationException>(() =>
            sut.ExecuteAsync(MakeRun(), MakeProject()));

        Assert.Equal("run1", ex.TestRunId);
        _testRunRepository.Verify(r => r.UpdateAsync(
            It.Is<TestRun>(t => t.Status == TestRunStatus.Failed), CancellationToken.None), Times.Once);
        _scenarioRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyScenarioList_FailsRunAndThrows()
    {
        SetupSecretResolver();
        SetupJiraStoryContent();
        SetupChatResponse("[]");
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<ScenarioGenerationException>(() =>
            sut.ExecuteAsync(MakeRun(), MakeProject()));

        Assert.Equal("run1", ex.TestRunId);
        _testRunRepository.Verify(r => r.UpdateAsync(
            It.Is<TestRun>(t => t.Status == TestRunStatus.Failed), CancellationToken.None), Times.Once);
        _scenarioRepository.Verify(r => r.CreateBatchAsync(It.IsAny<IEnumerable<TestScenario>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ClaudeApiThrows_FailsRunAndThrows()
    {
        SetupSecretResolver();
        SetupJiraStoryContent();
        _chatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<ScenarioGenerationException>(() =>
            sut.ExecuteAsync(MakeRun(), MakeProject()));

        Assert.Equal("run1", ex.TestRunId);
        Assert.IsType<HttpRequestException>(ex.InnerException);
        _testRunRepository.Verify(r => r.UpdateAsync(
            It.Is<TestRun>(t => t.Status == TestRunStatus.Failed), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FailedRunHasErrorDetailInSkipReason()
    {
        SetupSecretResolver();
        _jiraStoryClient
            .Setup(c => c.GetStoryContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JiraStoryContent?)null);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        var sut = CreateSut();
        await Assert.ThrowsAsync<ScenarioGenerationException>(() =>
            sut.ExecuteAsync(MakeRun(), MakeProject()));

        _testRunRepository.Verify(r => r.UpdateAsync(
            It.Is<TestRun>(t => t.SkipReason != null && t.SkipReason.Contains("scenario generation error")),
            CancellationToken.None), Times.Once);
    }
}
