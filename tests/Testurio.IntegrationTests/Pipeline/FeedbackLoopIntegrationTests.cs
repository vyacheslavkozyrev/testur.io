using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the FeedbackLoop stage (feature 0031 T019).
/// Exercises the full <see cref="Testurio.Pipeline.FeedbackLoop.FeedbackLoop.ProcessAsync"/>
/// orchestration path with mocked dependencies (no real Service Bus / Cosmos / PM tool).
///
/// Scenario coverage:
/// - ADO comment with flag → TestMemory upserted, confirmation comment posted.
/// - Jira comment with flag → same.
/// - Comment without flag → no Cosmos write, no reply.
/// - Second comment on same workItem + testType → repository called again (upsert semantics).
/// - No prior TestRun → no write, no reply (warning logged).
/// - Embedding service unavailable → exception propagates (message must not be settled).
/// </summary>
public class FeedbackLoopIntegrationTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<IEmbeddingService> _embeddingService = new();
    private readonly Mock<ITestMemoryRepository> _testMemoryRepo = new();
    private readonly Mock<IJiraApiClient> _jiraClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();

    private Testurio.Pipeline.FeedbackLoop.FeedbackLoop CreateSut() =>
        new(_testRunRepo.Object, _projectRepo.Object, _embeddingService.Object,
            _testMemoryRepo.Object, _jiraClient.Object, _adoClient.Object,
            _secretResolver.Object,
            NullLogger<Testurio.Pipeline.FeedbackLoop.FeedbackLoop>.Instance);

    private static readonly Guid ProjectId = Guid.Parse("12345678-1234-1234-1234-123456789012");

    // ─── ADO comment with flag → upsert + confirmation posted ────────────────

    [Fact]
    public async Task AdoCommentWithFlag_UpsertsMemoryAndPostsConfirmation()
    {
        var project = MakeAdoProject();
        SetupProjectAndToken(project);
        SetupLastRun(["api"]);
        SetupEmbeddingAndUpsert();
        _adoClient.Setup(a => a.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ado-comment-1");

        var sut = CreateSut();
        var evt = new CommentWebhookEvent
        {
            PmTool = "ado",
            WorkItemId = "42",
            CommentBody = "Always check empty state. @testurio memorize",
            CommentId = "ado-c-1",
            ProjectId = ProjectId,
        };

        await sut.ProcessAsync(evt, CancellationToken.None);

        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            project.UserId, ProjectId, "api", "Always check empty state.",
            It.IsAny<float[]>(), "42", "ado-c-1", It.IsAny<CancellationToken>()), Times.Once);

        _adoClient.Verify(a => a.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), 42,
            It.IsAny<string>(), It.Is<string>(b => b.Contains("Feedback captured")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Jira comment with flag → upsert + confirmation posted ───────────────

    [Fact]
    public async Task JiraCommentWithFlag_UpsertsMemoryAndPostsConfirmation()
    {
        var project = MakeJiraProject();
        SetupProjectAndToken(project);
        SetupLastRun(["uie2e"]);
        SetupEmbeddingAndUpsert();
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("jira-reply-1"));

        var sut = CreateSut();
        var evt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = "PROJ-100",
            CommentBody = "Test with SSO users. @testurio memorize",
            CommentId = "jira-c-1",
            ProjectId = ProjectId,
        };

        await sut.ProcessAsync(evt, CancellationToken.None);

        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            project.UserId, ProjectId, "uie2e", "Test with SSO users.",
            It.IsAny<float[]>(), "PROJ-100", "jira-c-1", It.IsAny<CancellationToken>()), Times.Once);

        _jiraClient.Verify(j => j.PostCommentAsync(
            It.IsAny<string>(), "PROJ-100", It.IsAny<string>(),
            It.IsAny<string>(), It.Is<string>(b => b.Contains("Feedback captured")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Comment without flag → no Cosmos write, no reply ────────────────────

    [Fact]
    public async Task CommentWithoutFlag_NoCosmosWriteNoReply()
    {
        var sut = CreateSut();
        var evt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = "PROJ-200",
            CommentBody = "This is a plain comment — no flag here.",
            CommentId = "c-plain",
            ProjectId = ProjectId,
        };

        await sut.ProcessAsync(evt, CancellationToken.None);

        _testMemoryRepo.VerifyNoOtherCalls();
        _jiraClient.VerifyNoOtherCalls();
        _adoClient.VerifyNoOtherCalls();
    }

    // ─── Second comment on same workItem + testType → repository called again ─

    [Fact]
    public async Task SecondCommentSameWorkItemAndTestType_UpsertCalledTwice()
    {
        var project = MakeJiraProject();
        SetupProjectAndToken(project);
        SetupLastRun(["api"]);
        SetupEmbeddingAndUpsert();
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();

        var firstEvt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = "PROJ-300",
            CommentBody = "First note. @testurio memorize",
            CommentId = "c-first",
            ProjectId = ProjectId,
        };

        var secondEvt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = "PROJ-300",
            CommentBody = "Updated note. @testurio memorize",
            CommentId = "c-second",
            ProjectId = ProjectId,
        };

        await sut.ProcessAsync(firstEvt, CancellationToken.None);
        await sut.ProcessAsync(secondEvt, CancellationToken.None);

        // Both calls go through — upsert handles de-duplication (overwrite existing doc).
        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), ProjectId, "api", It.IsAny<string>(),
            It.IsAny<float[]>(), "PROJ-300", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ─── No prior TestRun → no write, no reply ───────────────────────────────

    [Fact]
    public async Task NoPriorTestRun_NoWriteNoReply()
    {
        var project = MakeJiraProject();
        SetupProjectAndToken(project);
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun?)null);

        var sut = CreateSut();
        var evt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = "PROJ-404",
            CommentBody = "Something useful. @testurio memorize",
            CommentId = "c-norun",
            ProjectId = ProjectId,
        };

        await sut.ProcessAsync(evt, CancellationToken.None);

        _testMemoryRepo.VerifyNoOtherCalls();
        _jiraClient.VerifyNoOtherCalls();
    }

    // ─── Embedding service unavailable → exception propagates ────────────────

    [Fact]
    public async Task EmbeddingServiceUnavailable_ExceptionPropagates()
    {
        var project = MakeJiraProject();
        SetupProjectAndToken(project);
        SetupLastRun(["api"]);
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Azure OpenAI timeout"));

        var sut = CreateSut();
        var evt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = "PROJ-500",
            CommentBody = "Edge case with slow response. @testurio memorize",
            CommentId = "c-embed-fail",
            ProjectId = ProjectId,
        };

        // AC-017: exception must propagate so the SB message is not settled.
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessAsync(evt, CancellationToken.None));

        _testMemoryRepo.VerifyNoOtherCalls();
    }

    // ─── Fixtures ─────────────────────────────────────────────────────────────

    private void SetupProjectAndToken(Project project)
    {
        _projectRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("resolved-secret");
    }

    private void SetupLastRun(string[] testTypes)
    {
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRun
            {
                ProjectId = ProjectId.ToString(),
                UserId = "user-z",
                JiraIssueKey = "PROJ-100",
                JiraIssueId = "10000",
                ResolvedTestTypes = testTypes,
            });
    }

    private void SetupEmbeddingAndUpsert()
    {
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Project MakeJiraProject() => new()
    {
        UserId = "user-z",
        Name = "Integration Test Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "both",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://jira.example.com",
        JiraEmailSecretUri = "https://vault/jira-email",
        JiraApiTokenSecretUri = "https://vault/jira-token",
    };

    private static Project MakeAdoProject() => new()
    {
        UserId = "user-z",
        Name = "Integration Test ADO Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "both",
        PmTool = PMToolType.Ado,
        AdoOrgUrl = "https://dev.azure.com/myorg",
        AdoProjectName = "MyProject",
        AdoTokenSecretUri = "https://vault/ado-token",
    };
}
