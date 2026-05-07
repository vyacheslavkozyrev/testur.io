using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Moq.Protected;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;
using Testurio.Plugins.StoryParserPlugin;
using Testurio.Plugins.TestExecutorPlugin;
using Testurio.Plugins.TestGeneratorPlugin;
using Testurio.Worker.Steps;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests that exercise the full pipeline:
/// ScenarioGenerationStep (feature 0002) → ApiTestExecutionStep (feature 0003).
/// All external boundaries (Jira API, LLM, HTTP product API, Cosmos DB) are replaced with mocks.
/// </summary>
public class TestRunPipelineTests
{
    private readonly Mock<IJiraStoryClient> _jiraClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IChatCompletionService> _chatCompletion = new();
    private readonly Mock<ITestScenarioRepository> _scenarioRepository = new();
    private readonly Mock<IStepResultRepository> _stepResultRepository = new();
    private readonly Mock<ITestRunRepository> _testRunRepository = new();
    private readonly Mock<HttpMessageHandler> _httpHandler = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private ScenarioGenerationStep CreateGenerationStep() => new(
        _jiraClient.Object,
        _secretResolver.Object,
        new StoryParserPlugin(),
        new TestGeneratorPlugin(_chatCompletion.Object, NullLogger<TestGeneratorPlugin>.Instance),
        _scenarioRepository.Object,
        _testRunRepository.Object,
        NullLogger<ScenarioGenerationStep>.Instance);

    private ApiTestExecutionStep CreateExecutionStep()
    {
        var httpClient = new HttpClient(_httpHandler.Object);
        var validator = new ResponseSchemaValidator();
        var plugin = new TestExecutorPlugin(httpClient, validator, NullLogger<TestExecutorPlugin>.Instance);
        var credentialClient = new KeyVaultCredentialClient(_secretResolver.Object);
        return new ApiTestExecutionStep(
            plugin,
            credentialClient,
            _stepResultRepository.Object,
            _testRunRepository.Object,
            NullLogger<ApiTestExecutionStep>.Instance);
    }

    private static TestRun BuildTestRun() => new()
    {
        Id = "run-1",
        ProjectId = "proj-1",
        UserId = "user-1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = TestRunStatus.Active
    };

    private static Project BuildProject(string? bearerTokenRef = null) => new()
    {
        Id = "proj-1",
        UserId = "user-1",
        Name = "Test Project",
        ProductUrl = "https://api.example.com",
        JiraBaseUrl = "https://jira.example.com",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "ref://jira-token",
        JiraWebhookSecretRef = "ref://webhook",
        InTestingStatusLabel = "In Testing",
        BearerTokenSecretRef = bearerTokenRef
    };

    private void SetupJiraStory(string description = "User story description", string ac = "AC-001: something")
    {
        _jiraClient
            .Setup(c => c.GetStoryContentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraStoryContent { Description = description, AcceptanceCriteria = ac });
    }

    private void SetupSecretResolver(string returnValue = "test-token")
    {
        _secretResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnValue);
    }

    private void SetupLlmResponse(string json)
    {
        var chatMessage = new Microsoft.SemanticKernel.ChatMessageContent(
            Microsoft.SemanticKernel.AuthorRole.Assistant, json);
        _chatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<Microsoft.SemanticKernel.PromptExecutionSettings>(),
                It.IsAny<Microsoft.SemanticKernel.Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatMessage]);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string body = "{}")
    {
        _httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private void SetupRepositories()
    {
        _scenarioRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<TestScenario>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stepResultRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _testRunRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);
    }

    [Fact]
    public async Task Pipeline_TriggerGenerateExecute_AllStepsPass_RunStatusIsCompleted()
    {
        // Arrange
        var testRun = BuildTestRun();
        var project = BuildProject();
        SetupJiraStory();
        SetupSecretResolver("jira-api-token");
        SetupLlmResponse("""
            [{"title":"GET health check","steps":[{"order":1,"description":"Send GET /health","expectedResult":"HTTP 200 OK"}]}]
            """);
        SetupHttpResponse(HttpStatusCode.OK, "{}");
        SetupRepositories();

        var generationStep = CreateGenerationStep();
        var executionStep = CreateExecutionStep();

        // Act — Step 1: generate scenarios from Jira story (feature 0002)
        var scenarios = await generationStep.ExecuteAsync(testRun, project);

        // Act — Step 2: execute API tests (feature 0003)
        var results = await executionStep.ExecuteAsync(testRun, project, scenarios);

        // Assert — scenarios generated
        Assert.NotEmpty(scenarios);
        Assert.Equal("GET health check", scenarios[0].Title);

        // Assert — steps executed and run status set
        Assert.NotEmpty(results);
        Assert.Equal(StepStatus.Passed, results[0].Status);
        Assert.Equal(TestRunStatus.Completed, testRun.Status);
    }

    [Fact]
    public async Task Pipeline_TriggerGenerateExecute_StepFails_RunStatusIsFailed()
    {
        // Arrange
        var testRun = BuildTestRun();
        var project = BuildProject();
        SetupJiraStory();
        SetupSecretResolver("jira-api-token");
        SetupLlmResponse("""
            [{"title":"POST orders","steps":[{"order":1,"description":"Send POST /orders","expectedResult":"HTTP 201 Created"}]}]
            """);
        // Product API returns 500 instead of expected 201
        SetupHttpResponse(HttpStatusCode.InternalServerError, """{"error":"server error"}""");
        SetupRepositories();

        var generationStep = CreateGenerationStep();
        var executionStep = CreateExecutionStep();

        // Act
        var scenarios = await generationStep.ExecuteAsync(testRun, project);
        var results = await executionStep.ExecuteAsync(testRun, project, scenarios);

        // Assert
        Assert.Equal(StepStatus.Failed, results[0].Status);
        Assert.Equal(TestRunStatus.Failed, testRun.Status);
        Assert.Equal(500, results[0].ActualStatusCode);
    }

    [Fact]
    public async Task Pipeline_MultipleScenarios_AllExecutedAndResultsPersisted()
    {
        // Arrange
        var testRun = BuildTestRun();
        var project = BuildProject();
        SetupJiraStory();
        SetupSecretResolver("jira-api-token");
        SetupLlmResponse("""
            [
              {"title":"Scenario A","steps":[{"order":1,"description":"GET /api/a","expectedResult":"HTTP 200"}]},
              {"title":"Scenario B","steps":[{"order":1,"description":"GET /api/b","expectedResult":"HTTP 200"}]}
            ]
            """);
        SetupHttpResponse(HttpStatusCode.OK, "{}");
        SetupRepositories();

        IEnumerable<StepResult>? persistedResults = null;
        _stepResultRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<StepResult>, CancellationToken>((results, _) => persistedResults = results)
            .Returns(Task.CompletedTask);

        var generationStep = CreateGenerationStep();
        var executionStep = CreateExecutionStep();

        // Act
        var scenarios = await generationStep.ExecuteAsync(testRun, project);
        var results = await executionStep.ExecuteAsync(testRun, project, scenarios);

        // Assert
        Assert.Equal(2, scenarios.Count);
        Assert.Equal(2, results.Count);
        Assert.NotNull(persistedResults);
        Assert.Equal(2, persistedResults!.Count());
        Assert.Equal(TestRunStatus.Completed, testRun.Status);
    }

    [Fact]
    public async Task Pipeline_BearerTokenConfigured_TokenInjectedInRequests()
    {
        // Arrange
        var testRun = BuildTestRun();
        var project = BuildProject(bearerTokenRef: "ref://api-token");
        SetupJiraStory();
        _secretResolver
            .Setup(r => r.ResolveAsync("ref://jira-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("jira-api-token");
        _secretResolver
            .Setup(r => r.ResolveAsync("ref://api-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("api-bearer-token");

        SetupLlmResponse("""
            [{"title":"Auth test","steps":[{"order":1,"description":"GET /api/protected","expectedResult":"HTTP 200"}]}]
            """);

        HttpRequestMessage? capturedRequest = null;
        _httpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });

        SetupRepositories();

        var generationStep = CreateGenerationStep();
        var executionStep = CreateExecutionStep();

        // Act
        var scenarios = await generationStep.ExecuteAsync(testRun, project);
        await executionStep.ExecuteAsync(testRun, project, scenarios);

        // Assert — Bearer token was injected (AC-006)
        Assert.NotNull(capturedRequest?.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("api-bearer-token", capturedRequest.Headers.Authorization.Parameter);
    }
}
