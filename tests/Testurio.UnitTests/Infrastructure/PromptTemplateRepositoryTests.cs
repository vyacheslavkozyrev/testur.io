using Moq;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="IPromptTemplateRepository"/> contract as seen by callers.
/// These tests verify call signatures and expected interactions via a mock — they do NOT
/// exercise <see cref="Testurio.Infrastructure.Cosmos.PromptTemplateRepository"/> directly.
/// Cosmos-specific behaviour (point-read by id, NotFound handling) is tested in the
/// integration test suite (<c>GeneratorsIntegrationTests.cs</c>).
/// </summary>
public class PromptTemplateRepositoryTests
{
    private readonly Mock<IPromptTemplateRepository> _repository = new();

    private static PromptTemplate MakeTemplate(string stage = "api_test_generator") => new()
    {
        Id = stage,
        Stage = stage,
        TemplateType = stage,
        Version = 1,
        Body = "You are an API test engineer.",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetAsync_ExistingStage_ReturnsDocument()
    {
        const string stage = "api_test_generator";
        var expected = MakeTemplate(stage);

        _repository
            .Setup(r => r.GetAsync(stage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _repository.Object.GetAsync(stage, CancellationToken.None);

        Assert.Equal(stage, result.Stage);
        Assert.Equal(1, result.Version);
        Assert.Equal("You are an API test engineer.", result.Body);
    }

    [Fact]
    public async Task GetAsync_MissingStage_ThrowsInvalidOperationException()
    {
        const string missingStage = "nonexistent_generator";

        _repository
            .Setup(r => r.GetAsync(missingStage, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                $"PromptTemplate '{missingStage}' not found in the PromptTemplates container."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.Object.GetAsync(missingStage, CancellationToken.None));

        Assert.Contains(missingStage, ex.Message);
    }

    [Fact]
    public async Task GetAsync_UiE2eStage_ReturnsCorrectDocument()
    {
        const string stage = "ui_e2e_test_generator";
        var expected = MakeTemplate(stage) with { Body = "You are a Playwright test engineer." };

        _repository
            .Setup(r => r.GetAsync(stage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _repository.Object.GetAsync(stage, CancellationToken.None);

        Assert.Equal(stage, result.Stage);
        Assert.Equal("You are a Playwright test engineer.", result.Body);
    }

    [Fact]
    public async Task GetAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repository
            .Setup(r => r.GetAsync(It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _repository.Object.GetAsync("api_test_generator", cts.Token));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTemplates()
    {
        var templates = new List<PromptTemplate>
        {
            MakeTemplate("story_parser"),
            MakeTemplate("agent_router"),
            MakeTemplate("api_test_generator")
        };

        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var result = await _repository.Object.GetAllAsync(CancellationToken.None);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_CallsRepositoryWithUpdatedTemplate()
    {
        var updated = MakeTemplate("story_parser") with { Version = 2, Body = "Updated body." };

        _repository
            .Setup(r => r.UpdateAsync(updated, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _repository.Object.UpdateAsync(updated, CancellationToken.None);

        _repository.Verify(r => r.UpdateAsync(updated, It.IsAny<CancellationToken>()), Times.Once);
    }
}
