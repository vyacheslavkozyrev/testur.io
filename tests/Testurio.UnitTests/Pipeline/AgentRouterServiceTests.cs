using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Pipeline.AgentRouter;

namespace Testurio.UnitTests.Pipeline;

public class AgentRouterServiceTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<ITestRunRepository> _testRunRepo = new();

    private AgentRouterService CreateSut()
    {
        var classifier = new StoryClassifier(_llmClient.Object, NullLogger<StoryClassifier>.Instance);
        var skipPoster = new SkipCommentPoster(
            _jiraApiClient.Object,
            _adoClient.Object,
            _secretResolver.Object,
            NullLogger<SkipCommentPoster>.Instance);

        _testRunRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        return new AgentRouterService(
            classifier,
            skipPoster,
            _testRunRepo.Object,
            NullLogger<AgentRouterService>.Instance);
    }

    private static ParsedStory MakeStory() => new()
    {
        Title = "Add item to cart",
        Description = "User adds a product to the shopping cart.",
        AcceptanceCriteria = new[] { "Cart total updates", "Item count increments" }
    };

    private static TestRun MakeRun() => new()
    {
        Id = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001"
    };

    private static Project MakeProject(TestType[]? testTypes = null) => new()
    {
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Both",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://example.atlassian.net",
        JiraEmailSecretUri = "kv://email",
        JiraApiTokenSecretUri = "kv://token",
        TestTypes = testTypes
    };

    private void SetupClassifierResponse(string[] types, string reason)
    {
        var typesJson = string.Join(", ", types.Select(t => $"\"{t}\""));
        var json = $$"""{ "test_types": [{{typesJson}}], "reason": "{{reason}}" }""";
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
    }

    // ─── classified types pass project-config filter ──────────────────────────

    [Fact]
    public async Task RouteAsync_ClassifiedTypesMatchProjectConfig_ReturnsResolvedTypes()
    {
        SetupClassifierResponse(new[] { "api" }, "API story.");
        var project = MakeProject(new[] { TestType.Api });

        var sut = CreateSut();
        var result = await sut.RouteAsync(MakeStory(), project, MakeRun());

        Assert.Single(result.ResolvedTestTypes);
        Assert.Equal(TestType.Api, result.ResolvedTestTypes[0]);
        Assert.Equal("API story.", result.ClassificationReason);
    }

    // ─── type absent from project config is excluded ──────────────────────────

    [Fact]
    public async Task RouteAsync_TypeNotInProjectConfig_IsExcluded()
    {
        // Claude suggests both, but project only has api configured.
        SetupClassifierResponse(new[] { "api", "ui_e2e" }, "Both detected.");
        var project = MakeProject(new[] { TestType.Api }); // only api configured

        var sut = CreateSut();
        var result = await sut.RouteAsync(MakeStory(), project, MakeRun());

        Assert.Single(result.ResolvedTestTypes);
        Assert.Equal(TestType.Api, result.ResolvedTestTypes[0]);
    }

    // ─── null project TestTypes defaults to all MVP types ────────────────────

    [Fact]
    public async Task RouteAsync_NullProjectTestTypes_DefaultsToAllMvpTypes()
    {
        SetupClassifierResponse(new[] { "api", "ui_e2e" }, "Both.");
        var project = MakeProject(null); // null defaults to api + ui_e2e

        var sut = CreateSut();
        var result = await sut.RouteAsync(MakeStory(), project, MakeRun());

        Assert.Equal(2, result.ResolvedTestTypes.Length);
    }

    // ─── empty after filter → skip comment posted, Skipped status set ─────────

    [Fact]
    public async Task RouteAsync_EmptyAfterFilter_SetsSkippedStatusAndPostsComment()
    {
        // Claude suggests ui_e2e only, but project only has api configured.
        SetupClassifierResponse(new[] { "ui_e2e" }, "UI story.");
        var project = MakeProject(new[] { TestType.Api }); // ui_e2e not in project config
        var run = MakeRun();

        _secretResolver.Setup(r => r.ResolveAsync("kv://email", It.IsAny<CancellationToken>()))
            .ReturnsAsync("qa@example.com");
        _secretResolver.Setup(r => r.ResolveAsync("kv://token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-value");
        _jiraApiClient
            .Setup(c => c.PostCommentAsync(
                It.IsAny<string>(), "PROJ-1", It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();
        var result = await sut.RouteAsync(MakeStory(), project, run);

        Assert.Empty(result.ResolvedTestTypes);
        Assert.Equal(TestRunStatus.Skipped, run.Status);
        Assert.NotNull(run.SkipReason);
        Assert.Contains("no applicable test type", run.SkipReason);

        _jiraApiClient.Verify(c =>
            c.PostCommentAsync(
                It.IsAny<string>(), "PROJ-1", It.IsAny<string>(),
                It.IsAny<string>(), It.Is<string>(s => s.Contains("Skipped")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── comment-post failure → pipeline continues ────────────────────────────

    [Fact]
    public async Task RouteAsync_CommentPostFails_PipelineContinues()
    {
        SetupClassifierResponse(Array.Empty<string>(), "No type.");
        var project = MakeProject(new[] { TestType.Api });
        var run = MakeRun();

        // Secret resolver throws — simulates Key Vault failure during comment posting.
        _secretResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault down"));

        var sut = CreateSut();

        // Must not throw — comment failure is swallowed.
        var result = await sut.RouteAsync(MakeStory(), project, run);

        Assert.Empty(result.ResolvedTestTypes);
        Assert.Equal(TestRunStatus.Skipped, run.Status);
    }

    // ─── two types → two generators returned ─────────────────────────────────

    [Fact]
    public async Task RouteAsync_BothTypes_RunRecordHasBothTypes()
    {
        SetupClassifierResponse(new[] { "api", "ui_e2e" }, "Both types.");
        var project = MakeProject(new[] { TestType.Api, TestType.UiE2e });
        var run = MakeRun();

        var sut = CreateSut();
        var result = await sut.RouteAsync(MakeStory(), project, run);

        Assert.Equal(2, result.ResolvedTestTypes.Length);
        Assert.Contains(TestType.Api, result.ResolvedTestTypes);
        Assert.Contains(TestType.UiE2e, result.ResolvedTestTypes);

        // Run record must be updated with string representation of both types.
        Assert.NotNull(run.ResolvedTestTypes);
        Assert.Equal(2, run.ResolvedTestTypes!.Length);
    }

    // ─── run metadata is persisted regardless of path ────────────────────────

    [Fact]
    public async Task RouteAsync_OnSuccessPath_PersistsRoutingMetadataToRunRecord()
    {
        SetupClassifierResponse(new[] { "api" }, "API reason.");
        var project = MakeProject(new[] { TestType.Api });
        var run = MakeRun();

        var sut = CreateSut();
        await sut.RouteAsync(MakeStory(), project, run);

        Assert.NotNull(run.ResolvedTestTypes);
        Assert.NotNull(run.ClassificationReason);
        Assert.Equal("API reason.", run.ClassificationReason);

        _testRunRepo.Verify(r => r.UpdateAsync(run, It.IsAny<CancellationToken>()), Times.Once);
    }
}
