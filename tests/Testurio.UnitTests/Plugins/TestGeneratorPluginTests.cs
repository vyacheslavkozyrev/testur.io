using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Testurio.Plugins.TestGeneratorPlugin;
using Xunit;

namespace Testurio.UnitTests.Plugins;

public class TestGeneratorPluginTests
{
    private readonly Mock<IChatCompletionService> _chatCompletion = new();

    private TestGeneratorPlugin CreateSut() =>
        new(_chatCompletion.Object, NullLogger<TestGeneratorPlugin>.Instance);

    private void SetupChatResponse(string content)
    {
        var chatMessage = new ChatMessageContent(AuthorRole.Assistant, content);
        _chatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatMessage]);
    }

    [Fact]
    public async Task GenerateAsync_ValidJsonResponse_ReturnsScenarios()
    {
        var json = """
            [
              {
                "title": "POST /orders returns 201",
                "steps": [
                  { "order": 1, "description": "Send POST /api/orders with valid body", "expectedResult": "HTTP 201 Created" }
                ]
              }
            ]
            """;
        SetupChatResponse(json);
        var sut = CreateSut();

        var result = await sut.GenerateAsync("run1", "proj1", "story input");

        Assert.Single(result);
        Assert.Equal("POST /orders returns 201", result[0].Title);
        Assert.Single(result[0].Steps);
        Assert.Equal("run1", result[0].TestRunId);
        Assert.Equal("proj1", result[0].ProjectId);
    }

    [Fact]
    public async Task GenerateAsync_MultipleScenarios_ReturnsAll()
    {
        var json = """
            [
              {
                "title": "Scenario A",
                "steps": [{ "order": 1, "description": "Step 1", "expectedResult": "Result 1" }]
              },
              {
                "title": "Scenario B",
                "steps": [
                  { "order": 1, "description": "Step 1", "expectedResult": "Result 1" },
                  { "order": 2, "description": "Step 2", "expectedResult": "Result 2" }
                ]
              }
            ]
            """;
        SetupChatResponse(json);
        var sut = CreateSut();

        var result = await sut.GenerateAsync("run1", "proj1", "story input");

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[1].Steps.Count);
    }

    [Fact]
    public async Task GenerateAsync_EmptyArray_ReturnsEmptyList()
    {
        SetupChatResponse("[]");
        var sut = CreateSut();

        var result = await sut.GenerateAsync("run1", "proj1", "story input");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateAsync_EmptyStringResponse_ReturnsEmptyList()
    {
        SetupChatResponse(string.Empty);
        var sut = CreateSut();

        var result = await sut.GenerateAsync("run1", "proj1", "story input");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateAsync_InvalidJson_ReturnsEmptyList()
    {
        SetupChatResponse("this is not json");
        var sut = CreateSut();

        var result = await sut.GenerateAsync("run1", "proj1", "story input");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateAsync_StepsAreOrderedByOrderField()
    {
        var json = """
            [
              {
                "title": "Ordered steps",
                "steps": [
                  { "order": 3, "description": "Third", "expectedResult": "Result 3" },
                  { "order": 1, "description": "First", "expectedResult": "Result 1" },
                  { "order": 2, "description": "Second", "expectedResult": "Result 2" }
                ]
              }
            ]
            """;
        SetupChatResponse(json);
        var sut = CreateSut();

        var result = await sut.GenerateAsync("run1", "proj1", "story input");

        var steps = result[0].Steps;
        Assert.Equal(1, steps[0].Order);
        Assert.Equal("First", steps[0].Description);
        Assert.Equal(2, steps[1].Order);
        Assert.Equal(3, steps[2].Order);
    }

    [Fact]
    public async Task GenerateAsync_ChatCompletionThrows_PropagatesException()
    {
        _chatCompletion
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var sut = CreateSut();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.GenerateAsync("run1", "proj1", "story input"));
    }
}
