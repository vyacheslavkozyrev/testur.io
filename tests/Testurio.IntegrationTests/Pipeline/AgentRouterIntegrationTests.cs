using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Pipeline.AgentRouter;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the AgentRouter pipeline stage (feature 0026).
/// Exercises the full routing path through <see cref="AgentRouterService"/> with mocked
/// Anthropic and PM tool clients. Validates end-to-end behaviour from <see cref="ParsedStory"/>
/// input to routing metadata written on the <see cref="TestRun"/> record.
/// </summary>
public class AgentRouterIntegrationTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<ITestRunRepository> _testRunRepo = new();

    private AgentRouterService CreateAgentRouterService()
    {
        var apiMock = new Mock<ITestGeneratorAgent>();
        var uiMock = new Mock<ITestGeneratorAgent>();

        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestGeneratorAgent>(TestGeneratorFactory.ApiKey, (_, _) => apiMock.Object);
        services.AddKeyedSingleton<ITestGeneratorAgent>(TestGeneratorFactory.UiE2eKey, (_, _) => uiMock.Object);
        var sp = services.BuildServiceProvider();

        var classifier = new StoryClassifier(_llmClient.Object, NullLogger<StoryClassifier>.Instance);
        var skipPoster = new SkipCommentPoster(
            _jiraApiClient.Object,
            _adoClient.Object,
            _secretResolver.Object,
            NullLogger<SkipCommentPoster>.Instance);
        var factory = new TestGeneratorFactory(sp);

        _testRunRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        return new AgentRouterService(
            classifier,
            skipPoster,
            factory,
            _testRunRepo.Object,
            NullLogger<AgentRouterService>.Instance);
    }

    private static ParsedStory MakeApiStory() => new()
    {
        Title = "Submit order via API",
        Description = "The user submits an order through the REST API.",
        AcceptanceCriteria = new[] { "POST /orders returns 201", "Order is persisted in database" }
    };

    private static ParsedStory MakeUiStory() => new()
    {
        Title = "View order confirmation page",
        Description = "After checkout, the user sees an order confirmation page.",
        AcceptanceCriteria = new[] { "Confirmation page shows order number", "Email sent notification visible" }
    };

    private static ParsedStory MakeInfrastructureStory() => new()
    {
        Title = "Configure CI/CD pipeline",
        Description = "Set up GitHub Actions for automated deployments.",
        AcceptanceCriteria = new[] { "Pipeline triggers on push to main" }
    };

    private static TestRun MakeRun() => new()
    {
        Id = "run-int-1",
        ProjectId = "proj-int-1",
        UserId = "user-int-1",
        JiraIssueKey = "INT-1",
        JiraIssueId = "20001"
    };

    private static Project MakeProject(TestType[]? testTypes = null) => new()
    {
        UserId = "user-int-1",
        Name = "Integration Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Both",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://example.atlassian.net",
        JiraEmailSecretUri = "kv://email-secret",
        JiraApiTokenSecretUri = "kv://token-secret",
        TestTypes = testTypes
    };

    private void SetupLlmResponse(string[] types, string reason)
    {
        var typesJson = string.Join(", ", types.Select(t => $"\"{t}\""));
        _llmClient
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($$"""{ "test_types": [{{typesJson}}], "reason": "{{reason}}" }""");
    }

    // ─── classifiable story → generators built end-to-end ────────────────────

    [Fact]
    public async Task FullRouting_ClassifiableApiStory_ReturnsApiTypeAndPersistsMetadata()
    {
        SetupLlmResponse(new[] { "api" }, "Story describes a REST API endpoint.");
        var run = MakeRun();
        var project = MakeProject();

        var sut = CreateAgentRouterService();
        var result = await sut.RouteAsync(MakeApiStory(), project, run);

        // Result carries the resolved type.
        Assert.Single(result.ResolvedTestTypes);
        Assert.Equal(TestType.Api, result.ResolvedTestTypes[0]);
        Assert.Equal("Story describes a REST API endpoint.", result.ClassificationReason);

        // Run record is updated with routing metadata.
        Assert.NotNull(run.ResolvedTestTypes);
        Assert.Single(run.ResolvedTestTypes!);
        Assert.Equal("Api", run.ResolvedTestTypes[0]);
        Assert.Equal("Story describes a REST API endpoint.", run.ClassificationReason);

        // Run is NOT skipped.
        Assert.NotEqual(TestRunStatus.Skipped, run.Status);

        // Repository was called once to persist metadata.
        _testRunRepo.Verify(r => r.UpdateAsync(run, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── unclassifiable → Skipped status + comment posted ────────────────────

    [Fact]
    public async Task FullRouting_UnclassifiableStory_SetsSkippedStatusAndPostsComment()
    {
        SetupLlmResponse(Array.Empty<string>(), "Story describes infrastructure configuration, not testable behaviour.");
        var run = MakeRun();
        var project = MakeProject();

        _secretResolver.Setup(r => r.ResolveAsync("kv://email-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync("qa@example.com");
        _secretResolver.Setup(r => r.ResolveAsync("kv://token-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync("api-token-value");
        _jiraApiClient
            .Setup(c => c.PostCommentAsync(
                "https://example.atlassian.net", "INT-1",
                "qa@example.com", "api-token-value",
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateAgentRouterService();
        var result = await sut.RouteAsync(MakeInfrastructureStory(), project, run);

        // Result is empty.
        Assert.Empty(result.ResolvedTestTypes);
        Assert.NotEmpty(result.ClassificationReason);

        // Run is marked Skipped.
        Assert.Equal(TestRunStatus.Skipped, run.Status);
        Assert.NotNull(run.SkipReason);
        Assert.Contains("no applicable test type", run.SkipReason);

        // Skip comment was posted to Jira.
        _jiraApiClient.Verify(c =>
            c.PostCommentAsync(
                "https://example.atlassian.net", "INT-1",
                "qa@example.com", "api-token-value",
                It.Is<string>(s => s.Contains("Skipped") && s.Contains("infrastructure")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── both types resolved → two generator instances forwarded ─────────────

    [Fact]
    public async Task FullRouting_BothTypes_RunRecordHasBothTypesAndMetadataPersisted()
    {
        SetupLlmResponse(new[] { "api", "ui_e2e" }, "Story covers both API and UI interactions.");
        var run = MakeRun();
        var project = MakeProject(new[] { TestType.Api, TestType.UiE2e });

        var sut = CreateAgentRouterService();
        var result = await sut.RouteAsync(MakeUiStory(), project, run);

        // Both types resolved.
        Assert.Equal(2, result.ResolvedTestTypes.Length);
        Assert.Contains(TestType.Api, result.ResolvedTestTypes);
        Assert.Contains(TestType.UiE2e, result.ResolvedTestTypes);

        // Run record reflects both types.
        Assert.NotNull(run.ResolvedTestTypes);
        Assert.Equal(2, run.ResolvedTestTypes!.Length);

        // Classification reason is preserved.
        Assert.Equal("Story covers both API and UI interactions.", run.ClassificationReason);
    }

    // ─── project-config filtering works end-to-end ───────────────────────────

    [Fact]
    public async Task FullRouting_ProjectConfigExcludesUiE2e_FilteredTypeNotReturned()
    {
        // Claude suggests both, but project is configured for api only.
        SetupLlmResponse(new[] { "api", "ui_e2e" }, "Both detected.");
        var run = MakeRun();
        var project = MakeProject(new[] { TestType.Api }); // api only

        var sut = CreateAgentRouterService();
        var result = await sut.RouteAsync(MakeApiStory(), project, run);

        Assert.Single(result.ResolvedTestTypes);
        Assert.Equal(TestType.Api, result.ResolvedTestTypes[0]);
    }

    // ─── comment-post failure does not propagate ──────────────────────────────

    [Fact]
    public async Task FullRouting_CommentPostFailsDuringSkip_PipelineContinuesToCompletion()
    {
        SetupLlmResponse(Array.Empty<string>(), "Infrastructure change.");
        var run = MakeRun();
        var project = MakeProject();

        // Secret resolver throws — comment posting will fail.
        _secretResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unavailable"));

        var sut = CreateAgentRouterService();

        // Must not throw even though comment posting fails.
        var result = await sut.RouteAsync(MakeInfrastructureStory(), project, run);

        Assert.Empty(result.ResolvedTestTypes);
        Assert.Equal(TestRunStatus.Skipped, run.Status);
    }
}
