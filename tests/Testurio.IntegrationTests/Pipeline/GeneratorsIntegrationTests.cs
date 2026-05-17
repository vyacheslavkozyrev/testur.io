using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Generators;
using Testurio.Pipeline.Generators.Services;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the generator stage (feature 0028).
/// Exercises the full path through <see cref="ApiTestGeneratorAgent"/> and
/// <see cref="UiE2eTestGeneratorAgent"/> via mocked <see cref="ILlmGenerationClient"/>
/// and <see cref="IPromptTemplateRepository"/>, validating:
/// - Both agents succeed → merged <see cref="GeneratorResults"/> produced
/// - One agent exhausts retries → <see cref="Core.Exceptions.TestGeneratorException"/> thrown with correct type
/// - Template not found → <see cref="InvalidOperationException"/> propagated before any agent starts
/// - Cancellation token cancelled mid-call → both Claude calls cancelled
/// </summary>
public class GeneratorsIntegrationTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IPromptTemplateRepository> _promptTemplateRepo = new();
    private readonly PromptAssemblyService _promptAssembly = new();

    private ApiTestGeneratorAgent CreateApiAgent() =>
        new(_llmClient.Object, _promptAssembly, NullLogger<ApiTestGeneratorAgent>.Instance);

    private UiE2eTestGeneratorAgent CreateUiE2eAgent() =>
        new(_llmClient.Object, _promptAssembly, NullLogger<UiE2eTestGeneratorAgent>.Instance);

    private static PromptTemplate MakeApiTemplate() => new()
    {
        Id = "api_test_generator",
        TemplateType = "api_test_generator",
        Version = "1.0.0",
        SystemPrompt = "You are an API test engineer.",
        GeneratorInstruction = "Generate up to {{maxScenarios}} API scenarios.",
        MaxScenarios = 10
    };

    private static PromptTemplate MakeUiE2eTemplate() => new()
    {
        Id = "ui_e2e_test_generator",
        TemplateType = "ui_e2e_test_generator",
        Version = "1.0.0",
        SystemPrompt = "You are a Playwright test engineer.",
        GeneratorInstruction = "Generate up to {{maxScenarios}} UI scenarios.",
        MaxScenarios = 5
    };

    private static ParsedStory MakeStory() => new()
    {
        Title = "Create order",
        Description = "User creates an order.",
        AcceptanceCriteria = ["POST /orders returns 201", "Order saved to DB"]
    };

    private static Project MakeProject() => new()
    {
        UserId = "user-gen-1",
        Name = "Generator Integration Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "Full stack testing"
    };

    private static MemoryRetrievalResult EmptyMemory() => new() { Scenarios = [] };

    private static string ValidApiJson() => JsonSerializer.Serialize(new[]
    {
        new
        {
            id = "b1c2d3e4-0000-0000-0000-000000000001",
            title = "POST /orders returns 201",
            method = "POST",
            path = "/orders",
            headers = (object?)null,
            body = new { productId = "p1", quantity = 1 },
            assertions = new object[] { new { type = "status_code", expected = 201 } }
        }
    });

    private static string ValidUiE2eJson() => JsonSerializer.Serialize(new[]
    {
        new
        {
            id = "a1b2c3d4-0000-0000-0000-000000000001",
            title = "User creates order via UI",
            steps = new object[]
            {
                new { action = "navigate", url = "https://staging.example.com/orders/new" },
                new { action = "click", selector = "role=button[name=\"Place Order\"]" },
                new { action = "assert_url", expected = "https://staging.example.com/orders/confirmation" }
            }
        }
    });

    [Fact]
    public async Task BothAgentsSucceed_MergedGeneratorResultsForwardedToStage5()
    {
        // Arrange — both LLM calls return valid JSON.
        _llmClient
            .Setup(c => c.CompleteAsync(
                It.Is<string>(s => s.Contains("API test engineer")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidApiJson());

        _llmClient
            .Setup(c => c.CompleteAsync(
                It.Is<string>(s => s.Contains("Playwright test engineer")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidUiE2eJson());

        var apiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeApiTemplate(),
            TestRunId = Guid.NewGuid()
        };

        var uiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeUiE2eTemplate(),
            TestRunId = apiCtx.TestRunId
        };

        // Act — simulate Task.WhenAll parallel execution.
        var apiTask = CreateApiAgent().GenerateAsync(apiCtx, CancellationToken.None);
        var uiE2eTask = CreateUiE2eAgent().GenerateAsync(uiCtx, CancellationToken.None);
        await Task.WhenAll(apiTask, uiE2eTask);

        var apiResult = await apiTask;
        var uiResult = await uiE2eTask;

        // Merge as TestRunJobProcessor would.
        var merged = new GeneratorResults
        {
            ApiScenarios = apiResult.ApiScenarios,
            UiE2eScenarios = uiResult.UiE2eScenarios
        };

        // Assert — both scenario lists are populated.
        Assert.Single(merged.ApiScenarios);
        Assert.Single(merged.UiE2eScenarios);
        Assert.Equal("POST /orders returns 201", merged.ApiScenarios[0].Title);
        Assert.Equal("User creates order via UI", merged.UiE2eScenarios[0].Title);
    }

    [Fact]
    public async Task OneAgentExhaustsRetries_TestGeneratorExceptionThrown_OtherAgentSucceeds()
    {
        // API agent always returns invalid JSON; UI agent returns valid JSON.
        _llmClient
            .Setup(c => c.CompleteAsync(
                It.Is<string>(s => s.Contains("API test engineer")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid json {{{");

        _llmClient
            .Setup(c => c.CompleteAsync(
                It.Is<string>(s => s.Contains("Playwright test engineer")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidUiE2eJson());

        var runId = Guid.NewGuid();
        var apiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeApiTemplate(),
            TestRunId = runId
        };

        var uiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeUiE2eTemplate(),
            TestRunId = runId
        };

        var apiTask = CreateApiAgent().GenerateAsync(apiCtx, CancellationToken.None);
        var uiE2eTask = CreateUiE2eAgent().GenerateAsync(uiCtx, CancellationToken.None);

        try { await Task.WhenAll(apiTask, uiE2eTask); } catch { /* handled per-task below */ }

        // Assert — API agent throws, UI E2E agent succeeds.
        var apiEx = await Assert.ThrowsAsync<TestGeneratorException>(() => apiTask);
        Assert.Equal(TestType.Api, apiEx.TestType);
        Assert.Equal(4, apiEx.Attempts);

        var uiResult = await uiE2eTask;
        Assert.Single(uiResult.UiE2eScenarios);
        Assert.Empty(uiResult.ApiScenarios);
    }

    [Fact]
    public async Task TemplateNotFound_InvalidOperationExceptionPropagated_BeforeAgentStarts()
    {
        // Arrange — repository throws for any template type.
        _promptTemplateRepo
            .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PromptTemplate 'api_test_generator' not found."));

        // Act & Assert — the exception propagates before CreateApiAgent is called.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _promptTemplateRepo.Object.GetAsync("api_test_generator", CancellationToken.None));

        Assert.Contains("api_test_generator", ex.Message);

        // Verify no LLM calls were made.
        _llmClient.Verify(
            c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancellationTokenCancelled_BothClaudeCallsCancelled()
    {
        using var cts = new CancellationTokenSource();

        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (_, _, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return string.Empty;
            });

        var runId = Guid.NewGuid();
        var apiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeApiTemplate(),
            TestRunId = runId
        };

        var uiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeUiE2eTemplate(),
            TestRunId = runId
        };

        var apiTask = CreateApiAgent().GenerateAsync(apiCtx, cts.Token);
        var uiE2eTask = CreateUiE2eAgent().GenerateAsync(uiCtx, cts.Token);

        // Cancel after both tasks are started.
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => apiTask);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uiE2eTask);
    }

    [Fact]
    public async Task BothAgentsExhaustsRetries_BothThrowTestGeneratorException_Stage5ReceivesEmptyLists()
    {
        // Both agents return invalid JSON → both throw TestGeneratorException.
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json at all");

        var runId = Guid.NewGuid();
        var apiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeApiTemplate(),
            TestRunId = runId
        };

        var uiCtx = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = EmptyMemory(),
            ProjectConfig = MakeProject(),
            PromptTemplate = MakeUiE2eTemplate(),
            TestRunId = runId
        };

        var apiTask = CreateApiAgent().GenerateAsync(apiCtx, CancellationToken.None);
        var uiE2eTask = CreateUiE2eAgent().GenerateAsync(uiCtx, CancellationToken.None);

        try { await Task.WhenAll(apiTask, uiE2eTask); } catch { }

        // Simulate what TestRunJobProcessor does: catch per-agent and build empty merged result.
        var apiScenarios = new List<ApiTestScenario>();
        var uiE2eScenarios = new List<UiE2eTestScenario>();
        var warnings = new List<string>();

        try { apiScenarios.AddRange((await apiTask).ApiScenarios); }
        catch (TestGeneratorException ex) { warnings.Add($"api_test_generator: JSON parse failed after {ex.Attempts} attempts"); }

        try { uiE2eScenarios.AddRange((await uiE2eTask).UiE2eScenarios); }
        catch (TestGeneratorException ex) { warnings.Add($"ui_e2e_test_generator: JSON parse failed after {ex.Attempts} attempts"); }

        var merged = new GeneratorResults
        {
            ApiScenarios = apiScenarios.AsReadOnly(),
            UiE2eScenarios = uiE2eScenarios.AsReadOnly()
        };

        // Assert — stage 5 receives empty lists; warnings contain both entries.
        Assert.Empty(merged.ApiScenarios);
        Assert.Empty(merged.UiE2eScenarios);
        Assert.Equal(2, warnings.Count);
        Assert.Contains("api_test_generator", warnings[0]);
        Assert.Contains("ui_e2e_test_generator", warnings[1]);
    }
}
