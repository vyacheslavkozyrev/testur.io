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
/// test-type resolution behaviour (AC-007 through AC-011, feature 0031 T016).
/// </summary>
public class FeedbackLoopTestTypeTests
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

    private static CommentWebhookEvent MakeEvent(string body = "Note about the API. @testurio memorize") =>
        new()
        {
            PmTool = "jira",
            WorkItemId = "TEST-100",
            CommentBody = body,
            CommentId = "c99",
            ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };

    private static Project MakeProject() => new()
    {
        UserId = "user-x",
        Name = "My Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "api",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://jira.example.com",
        JiraEmailSecretUri = "https://vault/jira-email",
        JiraApiTokenSecretUri = "https://vault/jira-token",
    };

    private void SetupProjectAndSecrets()
    {
        _projectRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProject());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("secret");
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ─── AC-008 / AC-009: single testType → one upsert ───────────────────────

    [Fact]
    public async Task ProcessAsync_SingleResolvedTestType_OneUpsert()
    {
        SetupProjectAndSecrets();
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRun
            {
                ProjectId = "11111111-1111-1111-1111-111111111111",
                UserId = "user-x",
                JiraIssueKey = "TEST-100",
                JiraIssueId = "10100",
                ResolvedTestTypes = ["api"],
            });

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), CancellationToken.None);

        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AC-009: two testTypes → two upserts ─────────────────────────────────

    [Fact]
    public async Task ProcessAsync_TwoResolvedTestTypes_TwoUpserts()
    {
        SetupProjectAndSecrets();
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRun
            {
                ProjectId = "11111111-1111-1111-1111-111111111111",
                UserId = "user-x",
                JiraIssueKey = "TEST-100",
                JiraIssueId = "10100",
                ResolvedTestTypes = ["api", "uie2e"],
            });

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), CancellationToken.None);

        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ─── AC-010: no prior TestRun → warning logged, no upsert, no reply ──────

    [Fact]
    public async Task ProcessAsync_NoRunFound_NoUpsertNoReply()
    {
        SetupProjectAndSecrets();
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun?)null);

        var sut = CreateSut();
        await sut.ProcessAsync(MakeEvent(), CancellationToken.None);

        _embeddingService.VerifyNoOtherCalls();
        _testMemoryRepo.VerifyNoOtherCalls();
        _jiraClient.VerifyNoOtherCalls();
    }

    // ─── AC-017: embedding throws → rethrows ────────────────────────────────

    [Fact]
    public async Task ProcessAsync_EmbeddingThrows_Rethrows()
    {
        SetupProjectAndSecrets();
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRun
            {
                ProjectId = "11111111-1111-1111-1111-111111111111",
                UserId = "user-x",
                JiraIssueKey = "TEST-100",
                JiraIssueId = "10100",
                ResolvedTestTypes = ["api"],
            });
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Azure OpenAI unreachable"));

        var sut = CreateSut();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessAsync(MakeEvent(), CancellationToken.None));

        _testMemoryRepo.VerifyNoOtherCalls();
    }

    // ─── AC-018: upsert throws on first testType → rethrows, second not called

    [Fact]
    public async Task ProcessAsync_UpsertThrowsOnFirstType_RethrowsAndSkipsSecond()
    {
        SetupProjectAndSecrets();
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRun
            {
                ProjectId = "11111111-1111-1111-1111-111111111111",
                UserId = "user-x",
                JiraIssueKey = "TEST-100",
                JiraIssueId = "10100",
                ResolvedTestTypes = ["api", "uie2e"],
            });

        var callCount = 0;
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Cosmos write failed");
                return Task.CompletedTask;
            });

        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessAsync(MakeEvent(), CancellationToken.None));

        // Only the first upsert was called before the exception stopped the loop.
        Assert.Equal(1, callCount);
    }
}
