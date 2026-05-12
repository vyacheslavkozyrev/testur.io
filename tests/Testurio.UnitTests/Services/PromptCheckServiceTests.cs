using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Plugins.TestGeneratorPlugin;

namespace Testurio.UnitTests.Services;

public class PromptCheckServiceTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly PromptCheckService _sut;

    public PromptCheckServiceTests()
    {
        _sut = new PromptCheckService(_llmClient.Object, NullLogger<PromptCheckService>.Instance);
    }

    // ─── CheckAsync — valid response ────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_ReturnsStructuredFeedback_WhenLlmRespondsWithValidJson()
    {
        const string validJson =
            """
            {
              "clarity": { "assessment": "Clear and concise.", "suggestion": null },
              "specificity": { "assessment": "Specific enough.", "suggestion": "Add more detail." },
              "potentialConflicts": { "assessment": "No conflicts.", "suggestion": null }
            }
            """;
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validJson);

        var result = await _sut.CheckAsync("Always test auth edge cases.", "Focus on API contracts.");

        Assert.Equal("Clear and concise.", result.Clarity.Assessment);
        Assert.Null(result.Clarity.Suggestion);
        Assert.Equal("Add more detail.", result.Specificity.Suggestion);
        Assert.Equal("No conflicts.", result.PotentialConflicts.Assessment);
    }

    [Fact]
    public async Task CheckAsync_StripsMarkdownFences_BeforeDeserializing()
    {
        const string fencedJson =
            """
            ```json
            {
              "clarity": { "assessment": "OK", "suggestion": null },
              "specificity": { "assessment": "OK", "suggestion": null },
              "potentialConflicts": { "assessment": "OK", "suggestion": null }
            }
            ```
            """;
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fencedJson);

        var result = await _sut.CheckAsync("Test prompt.", "Strategy.");

        Assert.Equal("OK", result.Clarity.Assessment);
    }

    [Fact]
    public async Task CheckAsync_ThrowsInvalidOperationException_WhenLlmReturnsInvalidJson()
    {
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json at all");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CheckAsync("Test prompt.", "Strategy."));
    }

    [Fact]
    public async Task CheckAsync_PassesTestingStrategyInUserMessage()
    {
        const string strategy = "Focus on authentication flows.";
        const string prompt = "Always test with expired tokens.";
        const string validJson =
            """{"clarity":{"assessment":"OK","suggestion":null},"specificity":{"assessment":"OK","suggestion":null},"potentialConflicts":{"assessment":"OK","suggestion":null}}""";

        string? capturedUserMessage = null;
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, user, _) => capturedUserMessage = user)
            .ReturnsAsync(validJson);

        await _sut.CheckAsync(prompt, strategy);

        Assert.NotNull(capturedUserMessage);
        Assert.Contains(strategy, capturedUserMessage);
        Assert.Contains(prompt, capturedUserMessage);
    }
}

public class TestGeneratorPluginPromptCompositionTests
{
    // ─── ComposeSystemPrompt ─────────────────────────────────────────────────────

    [Fact]
    public void ComposeSystemPrompt_IncludesBasePrompt_Always()
    {
        var result = TestGeneratorPlugin.ComposeSystemPrompt("strategy", null);

        Assert.Contains("QA engineer", result);
    }

    [Fact]
    public void ComposeSystemPrompt_AppendsStrategy_WhenProvided()
    {
        const string strategy = "Focus on edge cases.";

        var result = TestGeneratorPlugin.ComposeSystemPrompt(strategy, null);

        Assert.Contains("Focus on edge cases.", result);
        Assert.Contains("Testing Strategy:", result);
    }

    [Fact]
    public void ComposeSystemPrompt_AppendsCustomPrompt_WhenProvided()
    {
        const string customPrompt = "Always test with invalid tokens.";

        var result = TestGeneratorPlugin.ComposeSystemPrompt("strategy", customPrompt);

        Assert.Contains("Always test with invalid tokens.", result);
        Assert.Contains("Additional Instructions:", result);
    }

    [Fact]
    public void ComposeSystemPrompt_OmitsCustomSection_WhenCustomPromptIsNull()
    {
        var result = TestGeneratorPlugin.ComposeSystemPrompt("strategy", null);

        Assert.DoesNotContain("Additional Instructions:", result);
    }

    [Fact]
    public void ComposeSystemPrompt_OmitsCustomSection_WhenCustomPromptIsWhitespace()
    {
        var result = TestGeneratorPlugin.ComposeSystemPrompt("strategy", "   ");

        Assert.DoesNotContain("Additional Instructions:", result);
    }

    [Fact]
    public void ComposeSystemPrompt_MaintainsOrder_SystemThenStrategyThenCustom()
    {
        const string strategy = "STRATEGY_MARKER";
        const string custom = "CUSTOM_MARKER";

        var result = TestGeneratorPlugin.ComposeSystemPrompt(strategy, custom);

        var strategyIndex = result.IndexOf(strategy, StringComparison.Ordinal);
        var customIndex = result.IndexOf(custom, StringComparison.Ordinal);

        Assert.True(strategyIndex < customIndex,
            "Testing strategy must appear before custom prompt in the composed output.");
    }

    [Fact]
    public async Task GenerateAsync_ThrowsPromptTooLongException_WhenComposedPromptExceedsLimit()
    {
        var llmClientMock = new Mock<ILlmGenerationClient>();
        var plugin = new TestGeneratorPlugin(llmClientMock.Object, NullLogger<TestGeneratorPlugin>.Instance);

        // Build a custom prompt that will push the composed prompt well over the token limit.
        var hugeCustomPrompt = new string('A', 15000);

        var ex = await Assert.ThrowsAsync<PromptTooLongException>(
            () => plugin.GenerateAsync(
                testRunId: "run-1",
                projectId: "proj-1",
                userId: "user-1",
                storyInput: "story",
                testingStrategy: "strategy",
                customPrompt: hugeCustomPrompt));

        Assert.Equal("run-1", ex.TestRunId);
    }
}
