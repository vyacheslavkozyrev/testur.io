using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.StoryParser;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the StoryParser pipeline stage (feature 0025).
/// Exercises the full parse path through StoryParserService with mocked Anthropic and PM tool clients.
/// Tests the complete stage from WorkItem input to ParsedStory output and PM tool comment posting.
/// </summary>
public class StoryParserIntegrationTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();

    private StoryParserService CreateStoryParserService()
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

    private static Project MakeJiraProject() => new()
    {
        UserId = "user1",
        Name = "Integration Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmailSecretUri = "kv://email-secret",
        JiraApiTokenSecretUri = "kv://token-secret"
    };

    // ─── Conformant story — direct path ─────────────────────────────────────────

    [Fact]
    public async Task FullParse_ConformantStory_ReturnsParsedStoryWithoutCallingClaude()
    {
        var workItem = new WorkItem
        {
            Title = "Add item to cart",
            Description = "As a user I want to add items to my cart so that I can purchase them later.",
            AcceptanceCriteria = "1. Cart total updates on add\n2. Item count increments\n3. Stock is not yet decremented",
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-1"
        };

        var sut = CreateStoryParserService();
        var result = await sut.ParseAsync(workItem, project: null);

        Assert.Equal("Add item to cart", result.Title);
        Assert.Equal(3, result.AcceptanceCriteria.Count);
        Assert.NotNull(result.Entities);
        Assert.NotNull(result.Actions);
        Assert.NotNull(result.EdgeCases);

        // Direct path — Claude must not be called
        _llmClient.Verify(c =>
            c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FullParse_ConformantStory_NoCommentPosted()
    {
        var workItem = new WorkItem
        {
            Title = "Submit order",
            Description = "User completes the checkout process.",
            AcceptanceCriteria = "- Order confirmation email sent",
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-10"
        };

        var sut = CreateStoryParserService();
        await sut.ParseAsync(workItem, MakeJiraProject());

        _jiraApiClient.Verify(c =>
            c.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Non-conformant story — AI-conversion path ──────────────────────────────

    [Fact]
    public async Task FullParse_NonConformantStory_CallsClaudeAndReturnsParsedStory()
    {
        var workItem = new WorkItem
        {
            Title = "Cart feature",
            Description = string.Empty,         // Fails template check
            AcceptanceCriteria = string.Empty,
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-2"
        };

        SetupValidClaudeResponse("AI Cart Story", "AI description of cart.", new[] { "Cart works" });

        var sut = CreateStoryParserService();
        var result = await sut.ParseAsync(workItem, project: null);

        Assert.Equal("AI Cart Story", result.Title);
        Assert.Single(result.AcceptanceCriteria);
        _llmClient.Verify(c =>
            c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FullParse_NonConformantStory_PostsWarningCommentToJira()
    {
        var workItem = new WorkItem
        {
            Title = "Some vague request",
            Description = string.Empty,
            AcceptanceCriteria = string.Empty,
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-3"
        };

        SetupValidClaudeResponse("Converted Title", "Converted description.", new[] { "AC one" });

        _secretResolver.Setup(r => r.ResolveAsync("kv://email-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync("qa@example.com");
        _secretResolver.Setup(r => r.ResolveAsync("kv://token-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync("api-token-value");
        _jiraApiClient.Setup(c =>
                c.PostCommentAsync(It.IsAny<string>(), "PROJ-3", It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateStoryParserService();
        await sut.ParseAsync(workItem, MakeJiraProject());

        _jiraApiClient.Verify(c =>
            c.PostCommentAsync(It.IsAny<string>(), "PROJ-3", It.IsAny<string>(),
                It.IsAny<string>(), It.Is<string>(s => s.Contains("template")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AI conversion failure ───────────────────────────────────────────────────

    [Fact]
    public async Task FullParse_NonConformantStory_ClaudeApiFails_ThrowsStoryParserException()
    {
        var workItem = new WorkItem
        {
            Title = "Some vague request",
            Description = string.Empty,
            AcceptanceCriteria = string.Empty,
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-4"
        };

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Anthropic unreachable"));

        var sut = CreateStoryParserService();

        await Assert.ThrowsAsync<StoryParserException>(() => sut.ParseAsync(workItem, project: null));
    }

    [Fact]
    public async Task FullParse_NonConformantStory_ClaudeReturnsInvalidJson_ThrowsStoryParserException()
    {
        var workItem = new WorkItem
        {
            Title = "Bad story",
            Description = string.Empty,
            AcceptanceCriteria = string.Empty,
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-5"
        };

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Not valid JSON at all");

        var sut = CreateStoryParserService();

        var ex = await Assert.ThrowsAsync<StoryParserException>(() => sut.ParseAsync(workItem, project: null));
        Assert.Contains("invalid AI response", ex.Message);
    }

    // ─── Comment-post failure — pipeline continuity ──────────────────────────────

    [Fact]
    public async Task FullParse_CommentPostFails_PipelineContinuesToCompletion()
    {
        var workItem = new WorkItem
        {
            Title = "Vague request",
            Description = string.Empty,
            AcceptanceCriteria = string.Empty,
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-6"
        };

        SetupValidClaudeResponse("Parsed Title", "Parsed description.", new[] { "AC one" });

        // Secret resolver throws — simulates Key Vault failure during comment posting
        _secretResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault down"));

        var sut = CreateStoryParserService();

        // Must not throw — comment failure is fire-and-forget
        var result = await sut.ParseAsync(workItem, MakeJiraProject());

        Assert.NotNull(result);
        Assert.Equal("Parsed Title", result.Title);
    }

    private void SetupValidClaudeResponse(string title, string description, string[] acceptanceCriteria)
    {
        var acJson = string.Join(", ", acceptanceCriteria.Select(ac => $"\"{ac}\""));
        var json = $$"""
            {
              "title": "{{title}}",
              "description": "{{description}}",
              "acceptance_criteria": [{{acJson}}],
              "entities": [],
              "actions": [],
              "edge_cases": []
            }
            """;
        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
    }
}
