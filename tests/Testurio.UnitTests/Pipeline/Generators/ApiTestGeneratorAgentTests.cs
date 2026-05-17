using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Generators;
using Testurio.Pipeline.Generators.Services;

namespace Testurio.UnitTests.Pipeline.Generators;

public class ApiTestGeneratorAgentTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<ILogger<ApiTestGeneratorAgent>> _logger = new();
    private readonly PromptAssemblyService _promptAssembly = new();

    public ApiTestGeneratorAgentTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private ApiTestGeneratorAgent CreateSut() =>
        new(_llmClient.Object, _promptAssembly, _logger.Object);

    private static GeneratorContext MakeContext() => new()
    {
        ParsedStory = new ParsedStory
        {
            Title = "Get user profile",
            Description = "Returns authenticated user profile.",
            AcceptanceCriteria = ["GET /profile returns 200"]
        },
        MemoryRetrievalResult = new MemoryRetrievalResult { Scenarios = [] },
        ProjectConfig = new Project
        {
            UserId = "user1",
            Name = "Test Project",
            ProductUrl = "https://app.example.com",
            TestingStrategy = "REST API focus"
        },
        PromptTemplate = new PromptTemplate
        {
            Id = "api_test_generator",
            TemplateType = "api_test_generator",
            Version = "1.0.0",
            SystemPrompt = "You are an API test engineer.",
            GeneratorInstruction = "Generate up to {{maxScenarios}} scenarios.",
            MaxScenarios = 10
        },
        TestRunId = Guid.NewGuid()
    };

    private static string ValidApiScenariosJson() => JsonSerializer.Serialize(new[]
    {
        new
        {
            id = Guid.NewGuid().ToString(),
            title = "GET /profile returns 200 for authenticated user",
            method = "GET",
            path = "/profile",
            headers = (object?)null,
            body = (object?)null,
            assertions = new object[]
            {
                new { type = "status_code", expected = 200 }
            }
        }
    });

    [Fact]
    public async Task GenerateAsync_ValidJson_ReturnsApiScenariosPopulated()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidApiScenariosJson());

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Single(result.ApiScenarios);
        Assert.Empty(result.UiE2eScenarios);
        Assert.Equal("GET /profile returns 200 for authenticated user", result.ApiScenarios[0].Title);
        Assert.Equal("GET", result.ApiScenarios[0].Method);
    }

    [Fact]
    public async Task GenerateAsync_UiE2eScenariosAlwaysEmpty_WhenSucceeds()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidApiScenariosJson());

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Empty(result.UiE2eScenarios);
    }

    [Fact]
    public async Task GenerateAsync_FirstAttemptInvalidThenValid_RetriesOnce()
    {
        var callCount = 0;
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? "not valid json {{{" : ValidApiScenariosJson();
            });

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Equal(2, callCount);
        Assert.Single(result.ApiScenarios);
    }

    [Fact]
    public async Task GenerateAsync_FourConsecutiveInvalidJson_ThrowsTestGeneratorException()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid json {{{");

        var ex = await Assert.ThrowsAsync<TestGeneratorException>(
            () => CreateSut().GenerateAsync(MakeContext(), CancellationToken.None));

        Assert.Equal(TestType.Api, ex.TestType);
        Assert.Equal(4, ex.Attempts);
        Assert.Contains("invalid json", ex.LastRawResponse);
    }

    [Fact]
    public async Task GenerateAsync_LogsWarning_OnEachRetryAttempt()
    {
        var callCount = 0;
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Return valid JSON on 3rd attempt so we get 2 retries.
                return callCount < 3 ? "bad json" : ValidApiScenariosJson();
            });

        await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        // 2 failed attempts → 2 warning logs
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateAsync_AcceptsMarkdownFencedJson()
    {
        var fencedJson = $"```json\n{ValidApiScenariosJson()}\n```";
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fencedJson);

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Single(result.ApiScenarios);
    }

    [Fact]
    public async Task GenerateAsync_CancellationToken_ForwardedToLlmClient()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateSut().GenerateAsync(MakeContext(), cts.Token));
    }
}
