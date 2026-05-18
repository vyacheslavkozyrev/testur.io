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
/// flag detection behaviour (AC-002, AC-003, AC-004, AC-005, feature 0031 T015).
/// </summary>
public class FeedbackLoopFlagDetectionTests
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

    private static CommentWebhookEvent MakeEvent(string body, string projectId = "00000000-0000-0000-0000-000000000001") =>
        new()
        {
            PmTool = "jira",
            WorkItemId = "TEST-123",
            CommentBody = body,
            CommentId = "c1",
            ProjectId = Guid.Parse(projectId),
        };

    // ─── AC-003: flag absent → no I/O calls ──────────────────────────────────

    [Fact]
    public async Task ProcessAsync_FlagAbsent_ExitsWithoutAnyIo()
    {
        var sut = CreateSut();
        var evt = MakeEvent("This is a plain comment with no flag.");

        await sut.ProcessAsync(evt, CancellationToken.None);

        _projectRepo.VerifyNoOtherCalls();
        _testRunRepo.VerifyNoOtherCalls();
        _embeddingService.VerifyNoOtherCalls();
        _testMemoryRepo.VerifyNoOtherCalls();
        _jiraClient.VerifyNoOtherCalls();
    }

    // ─── AC-004: flag present, non-empty feedbackText → embedding + upsert ───

    [Fact]
    public async Task ProcessAsync_FlagPresent_NonEmptyFeedback_CallsEmbeddingAndUpsert()
    {
        // Arrange
        var project = MakeProject();
        var lastRun = MakeRun(["api"]);
        _projectRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastRun);
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("reply-1"));
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("secret");

        var sut = CreateSut();
        var evt = MakeEvent("The login flow should also work with SSO. @testurio memorize");

        // Act
        await sut.ProcessAsync(evt, CancellationToken.None);

        // Assert: embedding called with the stripped, trimmed feedback text.
        _embeddingService.Verify(e => e.EmbedAsync(
            "The login flow should also work with SSO.", It.IsAny<CancellationToken>()), Times.Once);

        _testMemoryRepo.Verify(r => r.UpsertFeedbackAsync(
            project.UserId, It.IsAny<Guid>(), "api",
            "The login flow should also work with SSO.",
            It.IsAny<float[]>(), "TEST-123", "c1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── AC-004: flag present, empty feedbackText after strip → no I/O ───────

    [Fact]
    public async Task ProcessAsync_FlagPresentButEmptyRemainder_ExitsWithoutIo()
    {
        var sut = CreateSut();
        // Only the flag, nothing else.
        var evt = MakeEvent("@testurio memorize");

        await sut.ProcessAsync(evt, CancellationToken.None);

        _embeddingService.VerifyNoOtherCalls();
        _testMemoryRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_FlagPresentWithOnlyWhitespace_ExitsWithoutIo()
    {
        var sut = CreateSut();
        // Just whitespace around the flag.
        var evt = MakeEvent("  @testurio memorize  ");

        await sut.ProcessAsync(evt, CancellationToken.None);

        _embeddingService.VerifyNoOtherCalls();
        _testMemoryRepo.VerifyNoOtherCalls();
    }

    // ─── AC-002: case-insensitive match ──────────────────────────────────────

    [Theory]
    [InlineData("@Testurio Memorize important edge case")]
    [InlineData("@TESTURIO MEMORIZE important edge case")]
    [InlineData("@testurio MEMORIZE important edge case")]
    public async Task ProcessAsync_FlagCaseInsensitive_Matched(string commentBody)
    {
        // Arrange
        var project = MakeProject();
        var lastRun = MakeRun(["api"]);
        _projectRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastRun);
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("secret");

        var sut = CreateSut();
        var evt = MakeEvent(commentBody);

        // Act
        await sut.ProcessAsync(evt, CancellationToken.None);

        // Assert: embedding was called, proving the flag was detected.
        _embeddingService.Verify(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── AC-002: flag embedded mid-sentence → matched and stripped ───────────

    [Fact]
    public async Task ProcessAsync_FlagMidSentence_MatchedAndStripped()
    {
        // Arrange
        var project = MakeProject();
        var lastRun = MakeRun(["api"]);
        _projectRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _testRunRepo.Setup(r => r.GetMostRecentByWorkItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastRun);
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
        _testMemoryRepo.Setup(r => r.UpsertFeedbackAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jiraClient.Setup(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("secret");

        var sut = CreateSut();
        // Flag is in the middle — text before and after the flag should be preserved.
        var evt = MakeEvent("Part one @testurio memorize part two");

        await sut.ProcessAsync(evt, CancellationToken.None);

        // The stripped text is "Part one  part two" → trimmed to "Part one  part two".
        _embeddingService.Verify(e => e.EmbedAsync("Part one  part two", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Shared fixtures ──────────────────────────────────────────────────────

    private static Project MakeProject() => new()
    {
        UserId = "user-1",
        Name = "Test Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "api",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://jira.example.com",
        JiraEmailSecretUri = "https://vault/jira-email",
        JiraApiTokenSecretUri = "https://vault/jira-token",
    };

    private static TestRun MakeRun(string[] resolvedTestTypes) => new()
    {
        ProjectId = "00000000-0000-0000-0000-000000000001",
        UserId = "user-1",
        JiraIssueKey = "TEST-123",
        JiraIssueId = "10001",
        ResolvedTestTypes = resolvedTestTypes,
    };
}
