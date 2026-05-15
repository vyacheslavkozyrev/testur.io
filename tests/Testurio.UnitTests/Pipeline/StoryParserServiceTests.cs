using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.StoryParser;

namespace Testurio.UnitTests.Pipeline;

public class StoryParserServiceTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();

    private static WorkItem MakeConformantWorkItem() => new()
    {
        Title = "Add item to cart",
        Description = "User can add a product to their cart.",
        AcceptanceCriteria = "1. Cart total updates\n2. Item appears in cart",
        PmToolType = PMToolType.Jira,
        IssueKey = "PROJ-1"
    };

    private static WorkItem MakeNonConformantWorkItem() => new()
    {
        Title = "Some vague feature request",
        Description = string.Empty,   // Missing description — fails template check
        AcceptanceCriteria = string.Empty,
        PmToolType = PMToolType.Jira,
        IssueKey = "PROJ-2"
    };

    private static Project MakeProject() => new()
    {
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmailSecretUri = "email-uri",
        JiraApiTokenSecretUri = "token-uri"
    };

    private StoryParserService CreateSut()
    {
        var templateChecker = new TemplateChecker();
        var directParser = new DirectParser();
        var aiConverter = new AiStoryConverter(_llmClient.Object, NullLogger<AiStoryConverter>.Instance);
        var commentPoster = new PmToolCommentPoster(
            _jiraApiClient.Object,
            _adoClient.Object,
            _secretResolver.Object,
            NullLogger<PmToolCommentPoster>.Instance);

        return new StoryParserService(
            templateChecker,
            directParser,
            aiConverter,
            commentPoster,
            NullLogger<StoryParserService>.Instance);
    }

    [Fact]
    public async Task ParseAsync_ConformantStory_TakesDirectPathWithoutCallingClaude()
    {
        var sut = CreateSut();

        var result = await sut.ParseAsync(MakeConformantWorkItem(), project: null);

        Assert.Equal("Add item to cart", result.Title);
        _llmClient.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParseAsync_ConformantStory_DoesNotPostComment()
    {
        var sut = CreateSut();

        await sut.ParseAsync(MakeConformantWorkItem(), MakeProject());

        // Give any fire-and-forget tasks a moment to settle.
        await Task.Delay(50);
        _jiraApiClient.Verify(c => c.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParseAsync_NonConformantStory_CallsClaude()
    {
        SetupValidClaudeResponse();

        var sut = CreateSut();

        await sut.ParseAsync(MakeNonConformantWorkItem(), project: null);

        _llmClient.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParseAsync_NonConformantStory_ReturnsParsedStoryFromClaudeResponse()
    {
        SetupValidClaudeResponse();

        var sut = CreateSut();
        var result = await sut.ParseAsync(MakeNonConformantWorkItem(), project: null);

        Assert.Equal("AI-converted title", result.Title);
    }

    [Fact]
    public async Task ParseAsync_AiConversionFails_ThrowsStoryParserException()
    {
        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Claude unreachable"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<StoryParserException>(() =>
            sut.ParseAsync(MakeNonConformantWorkItem(), project: null));
    }

    [Fact]
    public async Task ParseAsync_CommentPostFails_PipelineContinues()
    {
        SetupValidClaudeResponse();

        _secretResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unreachable"));

        var sut = CreateSut();

        // Must not throw — comment-post failure is fire-and-forget
        var result = await sut.ParseAsync(MakeNonConformantWorkItem(), MakeProject());

        Assert.NotNull(result);
        Assert.Equal("AI-converted title", result.Title);
    }

    [Fact]
    public async Task ParseAsync_ConformantStory_ReturnsParsedStoryWithNonNullCollections()
    {
        var sut = CreateSut();

        var result = await sut.ParseAsync(MakeConformantWorkItem(), project: null);

        Assert.NotNull(result.AcceptanceCriteria);
        Assert.NotNull(result.Entities);
        Assert.NotNull(result.Actions);
        Assert.NotNull(result.EdgeCases);
    }

    private void SetupValidClaudeResponse()
    {
        var json = """
            {
              "title": "AI-converted title",
              "description": "AI-converted description.",
              "acceptance_criteria": ["Converted AC one"],
              "entities": [],
              "actions": [],
              "edge_cases": []
            }
            """;
        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
    }
}
