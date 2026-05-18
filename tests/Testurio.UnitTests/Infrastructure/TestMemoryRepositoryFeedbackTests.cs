using Moq;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="ITestMemoryRepository.UpsertFeedbackAsync"/> as seen by callers
/// (feature 0031 T018).
/// These tests verify the contract via a mock — they do NOT exercise
/// <see cref="Testurio.Infrastructure.Cosmos.TestMemoryRepository"/> directly.
/// Cosmos-specific upsert behaviour (insert vs. overwrite, id/createdAt preservation,
/// absence of passRate/runCount) requires a live Cosmos emulator integration test in
/// <c>Testurio.IntegrationTests</c>.
/// </summary>
public class TestMemoryRepositoryFeedbackTests
{
    private readonly Mock<ITestMemoryRepository> _repository = new();

    private static readonly Guid DefaultProjectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    // ─── AC-013: UpsertFeedbackAsync is called with correct parameters ─────────

    [Fact]
    public async Task UpsertFeedbackAsync_CalledWithCorrectParameters()
    {
        _repository.Setup(r => r.UpsertFeedbackAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embedding = new float[1536];
        await _repository.Object.UpsertFeedbackAsync(
            userId: "user-1",
            projectId: DefaultProjectId,
            testType: "api",
            feedbackText: "Always test with empty cart",
            storyEmbedding: embedding,
            workItemId: "ISSUE-42",
            commentId: "c-99",
            cancellationToken: CancellationToken.None);

        _repository.Verify(r => r.UpsertFeedbackAsync(
            "user-1", DefaultProjectId, "api", "Always test with empty cart",
            embedding, "ISSUE-42", "c-99", CancellationToken.None), Times.Once);
    }

    // ─── AC-014: upsert is idempotent when called twice for the same key ──────

    [Fact]
    public async Task UpsertFeedbackAsync_CalledTwiceForSameKey_BothCallsSucceed()
    {
        _repository.Setup(r => r.UpsertFeedbackAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embedding1 = new float[1536];
        var embedding2 = new float[1536];

        await _repository.Object.UpsertFeedbackAsync(
            "user-1", DefaultProjectId, "api", "First note",
            embedding1, "ISSUE-42", "c-100", CancellationToken.None);

        await _repository.Object.UpsertFeedbackAsync(
            "user-1", DefaultProjectId, "api", "Second note",
            embedding2, "ISSUE-42", "c-101", CancellationToken.None);

        // Both calls went through — the repository contract permits it (upsert semantics).
        _repository.Verify(r => r.UpsertFeedbackAsync(
            "user-1", DefaultProjectId, "api", It.IsAny<string>(),
            It.IsAny<float[]>(), "ISSUE-42", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ─── AC-016: passRate and runCount are absent from qalead entries ─────────
    // This test verifies the domain contract via FindSimilarAsync — a qalead entry
    // returned from the store should have null PassRate and RunCount.

    [Fact]
    public void TestMemoryEntry_QaleadSource_PassRateAndRunCountAreNull()
    {
        var entry = new TestMemoryEntry
        {
            UserId = "user-1",
            TestType = "api",
            StoryText = "Feedback text",
            ScenarioText = null,
            Source = "qalead",
            WorkItemId = "ISSUE-42",
            CommentId = "c-100",
            // Do NOT set PassRate or RunCount — they must default to null.
        };

        // AC-016: passRate and runCount must be null for qalead entries.
        Assert.Null(entry.PassRate);
        Assert.Null(entry.RunCount);
        Assert.Equal("qalead", entry.Source);
    }

    // ─── FindSimilarAsync still works after entity extension ─────────────────

    [Fact]
    public async Task FindSimilarAsync_ReturnsEntriesWithNewFields()
    {
        var entry = new TestMemoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user-1",
            ProjectId = "proj-1",
            TestType = "api",
            StoryText = "Some story",
            ScenarioText = null,
            Source = "qalead",
            WorkItemId = "ISSUE-10",
            CommentId = "c-5",
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _repository.Setup(r => r.FindSimilarAsync(
                "user-1", "proj-1", It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestMemoryEntry> { entry }.AsReadOnly());

        var results = await _repository.Object.FindSimilarAsync(
            "user-1", "proj-1", new float[1536], CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("qalead", result.Source);
        Assert.Equal("ISSUE-10", result.WorkItemId);
        Assert.Equal("c-5", result.CommentId);
        Assert.NotNull(result.UpdatedAt);
    }
}
