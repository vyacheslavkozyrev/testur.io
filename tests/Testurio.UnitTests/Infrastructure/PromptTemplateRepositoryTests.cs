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

    private static PromptTemplate MakeTemplate(string templateType = "api_test_generator") => new()
    {
        Id = templateType,
        TemplateType = templateType,
        Version = "1.0.0",
        SystemPrompt = "You are an API test engineer.",
        GeneratorInstruction = "Generate up to {{maxScenarios}} scenarios.",
        MaxScenarios = 10
    };

    [Fact]
    public async Task GetAsync_ExistingTemplateType_ReturnsDocument()
    {
        const string templateType = "api_test_generator";
        var expected = MakeTemplate(templateType);

        _repository
            .Setup(r => r.GetAsync(templateType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _repository.Object.GetAsync(templateType, CancellationToken.None);

        Assert.Equal(templateType, result.TemplateType);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal(10, result.MaxScenarios);
    }

    [Fact]
    public async Task GetAsync_MissingTemplateType_ThrowsInvalidOperationException()
    {
        const string missingType = "nonexistent_generator";

        _repository
            .Setup(r => r.GetAsync(missingType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                $"PromptTemplate '{missingType}' not found in the PromptTemplates container."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.Object.GetAsync(missingType, CancellationToken.None));

        Assert.Contains(missingType, ex.Message);
    }

    [Fact]
    public async Task GetAsync_UiE2eTemplateType_ReturnsCorrectDocument()
    {
        const string templateType = "ui_e2e_test_generator";
        var expected = MakeTemplate(templateType) with { MaxScenarios = 5 };

        _repository
            .Setup(r => r.GetAsync(templateType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _repository.Object.GetAsync(templateType, CancellationToken.None);

        Assert.Equal(templateType, result.TemplateType);
        Assert.Equal(5, result.MaxScenarios);
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
}
