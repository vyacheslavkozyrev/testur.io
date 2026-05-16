using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.StoryParser;

namespace Testurio.UnitTests.Pipeline;

public class AiStoryConverterTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();

    private AiStoryConverter CreateSut() =>
        new(_llmClient.Object, NullLogger<AiStoryConverter>.Instance);

    private static WorkItem MakeWorkItem() => new()
    {
        Title = "Some unformatted story",
        Description = "Does some thing.",
        AcceptanceCriteria = string.Empty,
        PmToolType = PMToolType.Jira,
        IssueKey = "PROJ-1"
    };

    [Fact]
    public async Task ConvertAsync_ValidResponse_ReturnsParsedStory()
    {
        var json = """
            {
              "title": "Add item to cart",
              "description": "User adds a product to cart.",
              "acceptance_criteria": ["Cart total updates", "Item appears in cart"],
              "entities": ["user", "cart"],
              "actions": ["add"],
              "edge_cases": []
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = CreateSut();
        var result = await sut.ConvertAsync(MakeWorkItem());

        Assert.Equal("Add item to cart", result.Title);
        Assert.Equal("User adds a product to cart.", result.Description);
        Assert.Equal(2, result.AcceptanceCriteria.Count);
        Assert.Contains("Cart total updates", result.AcceptanceCriteria);
        Assert.Contains("user", result.Entities);
        Assert.Empty(result.EdgeCases);
    }

    [Fact]
    public async Task ConvertAsync_ResponseWrappedInCodeFences_StillParses()
    {
        var json = """
            ```json
            {
              "title": "Add item to cart",
              "description": "User adds a product to cart.",
              "acceptance_criteria": ["Cart total updates"],
              "entities": [],
              "actions": [],
              "edge_cases": []
            }
            ```
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = CreateSut();
        var result = await sut.ConvertAsync(MakeWorkItem());

        Assert.Equal("Add item to cart", result.Title);
    }

    [Fact]
    public async Task ConvertAsync_MalformedJson_ThrowsStoryParserException()
    {
        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("this is not json at all");

        var sut = CreateSut();

        await Assert.ThrowsAsync<StoryParserException>(() => sut.ConvertAsync(MakeWorkItem()));
    }

    [Fact]
    public async Task ConvertAsync_MissingTitle_ThrowsStoryParserException()
    {
        var json = """
            {
              "title": "",
              "description": "Some description",
              "acceptance_criteria": ["AC one"],
              "entities": [],
              "actions": [],
              "edge_cases": []
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<StoryParserException>(() => sut.ConvertAsync(MakeWorkItem()));
        Assert.Contains("title", ex.Message);
    }

    [Fact]
    public async Task ConvertAsync_MissingDescription_ThrowsStoryParserException()
    {
        var json = """
            {
              "title": "A title",
              "description": "",
              "acceptance_criteria": ["AC one"],
              "entities": [],
              "actions": [],
              "edge_cases": []
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<StoryParserException>(() => sut.ConvertAsync(MakeWorkItem()));
        Assert.Contains("description", ex.Message);
    }

    [Fact]
    public async Task ConvertAsync_EmptyAcceptanceCriteria_ThrowsStoryParserException()
    {
        var json = """
            {
              "title": "A title",
              "description": "A description",
              "acceptance_criteria": [],
              "entities": [],
              "actions": [],
              "edge_cases": []
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<StoryParserException>(() => sut.ConvertAsync(MakeWorkItem()));
        Assert.Contains("acceptance_criteria", ex.Message);
    }

    [Fact]
    public async Task ConvertAsync_ClaudeApiThrows_ThrowsStoryParserException()
    {
        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unreachable"));

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<StoryParserException>(() => sut.ConvertAsync(MakeWorkItem()));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ConvertAsync_NullOptionalArrays_DefaultsToEmptyCollections()
    {
        var json = """
            {
              "title": "A title",
              "description": "A description",
              "acceptance_criteria": ["One AC"]
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var sut = CreateSut();
        var result = await sut.ConvertAsync(MakeWorkItem());

        Assert.NotNull(result.Entities);
        Assert.NotNull(result.Actions);
        Assert.NotNull(result.EdgeCases);
    }
}
