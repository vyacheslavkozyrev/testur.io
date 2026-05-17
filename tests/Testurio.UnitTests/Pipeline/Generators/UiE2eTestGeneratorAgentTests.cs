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

public class UiE2eTestGeneratorAgentTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<ILogger<UiE2eTestGeneratorAgent>> _logger = new();
    private readonly PromptAssemblyService _promptAssembly = new();

    public UiE2eTestGeneratorAgentTests()
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private UiE2eTestGeneratorAgent CreateSut() =>
        new(_llmClient.Object, _promptAssembly, _logger.Object);

    private static GeneratorContext MakeContext() => new()
    {
        ParsedStory = new ParsedStory
        {
            Title = "Login flow",
            Description = "User logs in with email and password.",
            AcceptanceCriteria = ["Redirect to dashboard after login"]
        },
        MemoryRetrievalResult = new MemoryRetrievalResult { Scenarios = [] },
        ProjectConfig = new Project
        {
            UserId = "user1",
            Name = "Test Project",
            ProductUrl = "https://app.example.com",
            TestingStrategy = "UI E2E focus"
        },
        PromptTemplate = new PromptTemplate
        {
            Id = "ui_e2e_test_generator",
            TemplateType = "ui_e2e_test_generator",
            Version = "1.0.0",
            SystemPrompt = "You are a Playwright test engineer.",
            GeneratorInstruction = "Generate up to {{maxScenarios}} UI scenarios.",
            MaxScenarios = 5
        },
        TestRunId = Guid.NewGuid()
    };

    private static string ValidUiE2eScenariosJson() => JsonSerializer.Serialize(new[]
    {
        new
        {
            id = Guid.NewGuid().ToString(),
            title = "Successful login redirects to dashboard",
            steps = new object[]
            {
                new { action = "navigate", url = "https://app.example.com/login" },
                new { action = "fill", selector = "role=textbox[name=\"Email\"]", value = "user@example.com" },
                new { action = "fill", selector = "role=textbox[name=\"Password\"]", value = "secret123" },
                new { action = "click", selector = "role=button[name=\"Log in\"]" },
                new { action = "assert_url", expected = "https://app.example.com/dashboard" }
            }
        }
    });

    [Fact]
    public async Task GenerateAsync_ValidJson_ReturnsUiE2eScenariosPopulated()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidUiE2eScenariosJson());

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Single(result.UiE2eScenarios);
        Assert.Empty(result.ApiScenarios);
        Assert.Equal("Successful login redirects to dashboard", result.UiE2eScenarios[0].Title);
        Assert.Equal(5, result.UiE2eScenarios[0].Steps.Count);
    }

    [Fact]
    public async Task GenerateAsync_ApiScenariosAlwaysEmpty_WhenSucceeds()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidUiE2eScenariosJson());

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Empty(result.ApiScenarios);
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
                return callCount == 1 ? "not valid json {{{" : ValidUiE2eScenariosJson();
            });

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Equal(2, callCount);
        Assert.Single(result.UiE2eScenarios);
    }

    [Fact]
    public async Task GenerateAsync_FourConsecutiveInvalidJson_ThrowsTestGeneratorException()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("this is definitely not json <<<");

        var ex = await Assert.ThrowsAsync<TestGeneratorException>(
            () => CreateSut().GenerateAsync(MakeContext(), CancellationToken.None));

        Assert.Equal(TestType.UiE2e, ex.TestType);
        Assert.Equal(4, ex.Attempts);
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
                return callCount < 3 ? "bad json" : ValidUiE2eScenariosJson();
            });

        await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

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
    public async Task GenerateAsync_ParsedSteps_ContainSelectorFormats()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidUiE2eScenariosJson());

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        var scenario = result.UiE2eScenarios[0];
        // Verify Playwright role locators are present in selectors (AC-021).
        var fillStep = scenario.Steps.OfType<FillStep>().First();
        Assert.Contains("role=", fillStep.Selector);
    }

    [Fact]
    public async Task GenerateAsync_AcceptsMarkdownFencedJson()
    {
        var fencedJson = $"```json\n{ValidUiE2eScenariosJson()}\n```";
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fencedJson);

        var result = await CreateSut().GenerateAsync(MakeContext(), CancellationToken.None);

        Assert.Single(result.UiE2eScenarios);
    }
}
