using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Services;

public class ReportTemplateServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly ReportTemplateService _sut;

    public ReportTemplateServiceTests()
    {
        _sut = new ReportTemplateService(
            _projectRepo.Object,
            _templateRepo.Object,
            NullLogger<ReportTemplateService>.Instance);
    }

    private static Project MakeProject(string? existingTemplateUri = null) => new()
    {
        Id = "proj-001",
        UserId = "user-1",
        Name = "Test",
        ProductUrl = "https://example.com",
        TestingStrategy = "API tests.",
        ReportTemplateUri = existingTemplateUri,
    };

    private static Stream MakeStream(string content = "# Report\n{{overall_result}}")
        => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

    // ─── Extension validation ─────────────────────────────────────────────────

    [Fact]
    public async Task UploadTemplateAsync_ReturnsFailure_WhenExtensionIsNotMd()
    {
        var result = await _sut.UploadTemplateAsync(
            "proj-001", "user-1", "report.txt", MakeStream(), 100);

        Assert.False(result.IsSuccess);
        Assert.Contains(".md", result.ErrorMessage!);
    }

    [Fact]
    public async Task UploadTemplateAsync_ReturnsFailure_WhenFileTooLarge()
    {
        var result = await _sut.UploadTemplateAsync(
            "proj-001", "user-1", "report.md", MakeStream(), ReportTemplateService.MaxTemplateSizeBytes + 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("100 KB", result.ErrorMessage!);
    }

    // ─── Successful upload ────────────────────────────────────────────────────

    [Fact]
    public async Task UploadTemplateAsync_ReturnsBlobUri_OnSuccess()
    {
        var project = MakeProject();
        _projectRepo
            .Setup(r => r.GetByIdAsync("user-1", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _templateRepo
            .Setup(r => r.UploadAsync("proj-001", "report.md", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.example.com/templates/proj-001/report.md");
        _projectRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _sut.UploadTemplateAsync(
            "proj-001", "user-1", "report.md", MakeStream(), 100);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://storage.example.com/templates/proj-001/report.md", result.BlobUri);
    }

    // ─── Token scanning ───────────────────────────────────────────────────────

    [Fact]
    public void ScanForUnknownTokens_ReturnsEmpty_WhenAllTokensKnown()
    {
        var content = "# {{overall_result}}\n{{story_title}}\n{{run_date}}";
        var warnings = ReportTemplateService.ScanForUnknownTokens(content);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ScanForUnknownTokens_ReturnsUnknownTokens()
    {
        var content = "# {{overall_result}}\n{{author}}\n{{build_number}}";
        var warnings = ReportTemplateService.ScanForUnknownTokens(content);
        Assert.Contains("{{author}}", warnings);
        Assert.Contains("{{build_number}}", warnings);
        Assert.DoesNotContain("{{overall_result}}", warnings);
    }

    [Fact]
    public void ScanForUnknownTokens_DeduplicatesWarnings()
    {
        var content = "{{author}} {{author}} {{overall_result}}";
        var warnings = ReportTemplateService.ScanForUnknownTokens(content);
        Assert.Single(warnings, w => w == "{{author}}");
    }

    [Fact]
    public void ScanForUnknownTokens_IsCaseSensitive()
    {
        // AC-020: {{Overall_Result}} is not a supported token.
        var content = "{{Overall_Result}}";
        var warnings = ReportTemplateService.ScanForUnknownTokens(content);
        Assert.Contains("{{Overall_Result}}", warnings);
    }

    // ─── Upload with warnings ─────────────────────────────────────────────────

    [Fact]
    public async Task UploadTemplateAsync_ReturnsWarnings_ForUnknownTokens()
    {
        var project = MakeProject();
        _projectRepo
            .Setup(r => r.GetByIdAsync("user-1", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _templateRepo
            .Setup(r => r.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.example.com/templates/proj-001/report.md");
        _projectRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var content = "# Report\n{{overall_result}}\n{{author}}";
        var result = await _sut.UploadTemplateAsync(
            "proj-001", "user-1", "report.md", MakeStream(content), content.Length);

        Assert.True(result.IsSuccess);
        Assert.Contains("{{author}}", result.Warnings);
    }

    // ─── Remove template ──────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveTemplateAsync_ReturnsFalse_WhenBlobDeletionFails()
    {
        var project = MakeProject("https://storage.example.com/old.md");
        _projectRepo
            .Setup(r => r.GetByIdAsync("user-1", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _templateRepo
            .Setup(r => r.DeleteAsync("https://storage.example.com/old.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.RemoveTemplateAsync("proj-001", "user-1");

        Assert.False(result);
        // AC-011: project document must NOT be updated when blob deletion fails.
        _projectRepo.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveTemplateAsync_ClearsUri_WhenBlobDeletedSuccessfully()
    {
        var project = MakeProject("https://storage.example.com/old.md");
        _projectRepo
            .Setup(r => r.GetByIdAsync("user-1", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _templateRepo
            .Setup(r => r.DeleteAsync("https://storage.example.com/old.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _projectRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _sut.RemoveTemplateAsync("proj-001", "user-1");

        Assert.True(result);
        _projectRepo.Verify(r => r.UpdateAsync(
            It.Is<Project>(p => p.ReportTemplateUri == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
