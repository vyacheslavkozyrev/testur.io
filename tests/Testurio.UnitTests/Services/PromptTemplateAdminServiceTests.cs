using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.UnitTests.Services;

public class PromptTemplateAdminServiceTests
{
    private readonly Mock<IPromptTemplateRepository> _repositoryMock = new();
    private readonly Mock<IPromptTemplateService> _promptTemplateServiceMock = new();
    private readonly IPromptTemplateAdminService _sut;

    public PromptTemplateAdminServiceTests()
    {
        _sut = new PromptTemplateAdminService(
            _repositoryMock.Object,
            _promptTemplateServiceMock.Object);
    }

    private static PromptTemplate MakeTemplate(
        string stage = "story_parser",
        int version = 1,
        string body = "You are a story parser.",
        bool isActive = true) =>
        new()
        {
            Id = stage,
            Stage = stage,
            TemplateType = stage,
            Version = version,
            Body = body,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    // ─── GetAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ExistingStage_ReturnsDto()
    {
        var template = MakeTemplate("story_parser");
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { template });

        var result = await _sut.GetAsync("story_parser");

        Assert.NotNull(result);
        Assert.Equal("story_parser", result.Stage);
        Assert.Equal(1, result.Version);
        Assert.Equal("You are a story parser.", result.Body);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetAsync_StageNotFound_ReturnsNull()
    {
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate>());

        var result = await _sut.GetAsync("story_parser");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_StageMatchIsCaseInsensitive()
    {
        var template = MakeTemplate("story_parser");
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { template });

        var result = await _sut.GetAsync("STORY_PARSER");

        Assert.NotNull(result);
        Assert.Equal("story_parser", result.Stage);
    }

    [Fact]
    public async Task GetAsync_MultipleTemplates_ReturnsCorrectOne()
    {
        var templates = new List<PromptTemplate>
        {
            MakeTemplate("story_parser"),
            MakeTemplate("agent_router", body: "You are an agent router."),
            MakeTemplate("api_test_generator", body: "You are an API test generator.")
        };
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var result = await _sut.GetAsync("agent_router");

        Assert.NotNull(result);
        Assert.Equal("agent_router", result.Stage);
        Assert.Equal("You are an agent router.", result.Body);
    }

    // ─── UpdateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingTemplate_ReturnsUpdatedDto()
    {
        var existing = MakeTemplate("story_parser", version: 1, body: "Old body.");
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { existing });
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PromptTemplate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _promptTemplateServiceMock
            .Setup(s => s.EvictAsync("story_parser", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.UpdateAsync("story_parser", "New body.");

        Assert.Equal("New body.", result.Body);
        Assert.Equal(2, result.Version);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_ExistingTemplate_CallsRepositoryUpdateWithCorrectTemplate()
    {
        var existing = MakeTemplate("story_parser", version: 3, body: "Old body.");
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { existing });

        PromptTemplate? savedTemplate = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PromptTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<PromptTemplate, CancellationToken>((t, _) => savedTemplate = t)
            .Returns(Task.CompletedTask);
        _promptTemplateServiceMock
            .Setup(s => s.EvictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync("story_parser", "Updated body.");

        Assert.NotNull(savedTemplate);
        Assert.Equal("Updated body.", savedTemplate.Body);
        Assert.Equal(4, savedTemplate.Version);
        Assert.True(savedTemplate.IsActive);
        Assert.Equal("story_parser", savedTemplate.Stage);
    }

    [Fact]
    public async Task UpdateAsync_ExistingTemplate_EvictsCacheAfterUpdate()
    {
        var existing = MakeTemplate("story_parser");
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { existing });
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PromptTemplate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _promptTemplateServiceMock
            .Setup(s => s.EvictAsync("story_parser", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync("story_parser", "New body.");

        _promptTemplateServiceMock.Verify(s => s.EvictAsync("story_parser", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_TemplateNotFound_ThrowsInvalidOperationException()
    {
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateAsync("story_parser", "Some body."));

        Assert.Contains("story_parser", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_SetsIsActiveTrueRegardlessOfPreviousValue()
    {
        // Even if the existing document had IsActive = false, UpdateAsync sets it to true.
        var existing = MakeTemplate("story_parser", isActive: false);
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { existing });

        PromptTemplate? savedTemplate = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PromptTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<PromptTemplate, CancellationToken>((t, _) => savedTemplate = t)
            .Returns(Task.CompletedTask);
        _promptTemplateServiceMock
            .Setup(s => s.EvictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.UpdateAsync("story_parser", "Re-activated body.");

        Assert.NotNull(savedTemplate);
        Assert.True(savedTemplate.IsActive);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_UpdatedAtIsRefreshed()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-5);
        var existing = MakeTemplate("story_parser") with { UpdatedAt = before };
        _repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromptTemplate> { existing });

        PromptTemplate? savedTemplate = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PromptTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<PromptTemplate, CancellationToken>((t, _) => savedTemplate = t)
            .Returns(Task.CompletedTask);
        _promptTemplateServiceMock
            .Setup(s => s.EvictAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync("story_parser", "New body.");

        Assert.NotNull(savedTemplate);
        Assert.True(savedTemplate.UpdatedAt > before);
    }
}
