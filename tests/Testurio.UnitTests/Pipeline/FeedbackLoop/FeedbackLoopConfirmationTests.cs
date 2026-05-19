using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Pipeline.FeedbackLoop;

/// <summary>
/// Unit tests for <see cref="Testurio.Pipeline.FeedbackLoop.FeedbackLoop"/> covering
/// confirmation comment posting behaviour (AC-019 through AC-023, feature 0031 T017).
/// </summary>
public class FeedbackLoopConfirmationTests
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

    private static CommentWebhookEvent MakeEvent(string body = "My note. @testurio memorize") =>
        new()
        {
            PmTool = "jira",
            WorkItemId = "TEST-200",
            CommentBody = body,
            CommentId = "c200",
            ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        };

    private Project MakeProject(PMToolType pmTool = PMToolType.Jira) => new()
    {
        UserId = "user-y",
        Name = "Confirmation Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "api",
        PmTool = pmTool,
        JiraBaseUrl = "https://jira.example.com",
        JiraEmailSecretUri = "https://vault/jira-email",
        JiraApiTokenSecretUri = "https://vault/jira-token",
    };

    private void SetupCommonMocks(string[] resolvedTypes, Project? project = null)
    {
        _projectRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project ?? MakeProject());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("secret");
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRun
            {
                ProjectId = "22222222-2222-2222-2222-222222222222",
                UserId = "user-y",
                JiraIssueKey = "TEST-200",
                JiraIssueId = "10200",
                ResolvedTestTypes = resolvedTypes,
            });
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ─── AC-021: all upserts succeed → PostCommentAsync called once ───────────

    [Fact]
    public async Task ProcessAsync_AllUpsertSucceed_PostCommentCalledOnce()
    {
        SetupCommonMocks(["api", "uie2e"]);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("reply-200"));

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), CancellationToken.None);

        _jiraClient.Verify(j => j.PostCommentAsync(
            It.IsAny<string>(), "TEST-200", It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AC-020: confirmation body contains both test types ───────────────────

    [Fact]
    public async Task ProcessAsync_TwoTestTypes_ConfirmationBodyContainsBothTypes()
    {
        SetupCommonMocks(["api", "uie2e"]);

        string? capturedBody = null;
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, body, _) => capturedBody = body)
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), CancellationToken.None);

        Assert.NotNull(capturedBody);
        Assert.Contains("api", capturedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uie2e", capturedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Feedback captured", capturedBody, StringComparison.OrdinalIgnoreCase);
    }

    // ─── AC-022: PostCommentAsync throws → warning logged, no rethrow ─────────

    [Fact]
    public async Task ProcessAsync_PostCommentThrows_DoesNotRethrow()
    {
        SetupCommonMocks(["api"]);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Jira unreachable"));

        var sut = CreateSut();

        // Should NOT throw — PM post failures are non-fatal (AC-022).
        await sut.ProcessAsync(MakeEvent(), CancellationToken.None);

        // Upsert was still called before the failed PM post.
        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AC-023: CancellationToken forwarded to PostCommentAsync ─────────────

    [Fact]
    public async Task ProcessAsync_ForwardsCancellationTokenToPostComment()
    {
        SetupCommonMocks(["api"]);

        using var cts = new CancellationTokenSource();
        CancellationToken? capturedToken = null;

        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string, CancellationToken>(
                (_, _, _, _, _, ct) => capturedToken = ct)
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    // ─── AC-023: CancellationToken forwarded to embedding ────────────────────

    [Fact]
    public async Task ProcessAsync_ForwardsCancellationTokenToEmbedding()
    {
        SetupCommonMocks(["api"]);

        using var cts = new CancellationTokenSource();
        CancellationToken? capturedToken = null;

        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, ct) => capturedToken = ct)
            .ReturnsAsync(new float[1536]);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    // ─── AC-023: CancellationToken forwarded to upsert ───────────────────────

    [Fact]
    public async Task ProcessAsync_ForwardsCancellationTokenToUpsert()
    {
        SetupCommonMocks(["api"]);

        using var cts = new CancellationTokenSource();
        CancellationToken? capturedToken = null;

        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, string, float[], string, string, CancellationToken>(
                (_, _, _, _, _, _, _, ct) => capturedToken = ct)
            .Returns(Task.CompletedTask);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }
}
