using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Constants;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Services;

public class ProjectServiceTests
{
    private readonly Mock<IProjectRepository> _repository = new();
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _sut = new ProjectService(_repository.Object, NullLogger<ProjectService>.Instance);
    }

    private static Project MakeProject(string userId = "user-1", string projectId = "proj-1") => new()
    {
        Id = projectId,
        UserId = userId,
        Name = "My App",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Focus on API contracts.",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    // ─── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsProjectDtos_ForUser()
    {
        var userId = "user-1";
        var projects = new[] { MakeProject(userId) };
        _repository.Setup(r => r.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

        var result = await _sut.ListAsync(userId);

        Assert.Single(result);
        Assert.Equal("My App", result[0].Name);
        Assert.Equal("proj-1", result[0].ProjectId);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyList_WhenNoProjects()
    {
        _repository.Setup(r => r.ListByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Project>());

        var result = await _sut.ListAsync("user-1");

        Assert.Empty(result);
    }

    // ─── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsDto_WhenProjectExists()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _sut.GetAsync("user-1", "proj-1");

        Assert.NotNull(result);
        Assert.Equal("proj-1", result.ProjectId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenProjectNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var result = await _sut.GetAsync("user-1", "proj-999");

        Assert.Null(result);
    }

    // ─── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsProject_WithCorrectFields()
    {
        var userId = "user-1";
        var request = new CreateProjectRequest("New Project", "https://new.example.com", "Smoke tests only.");

        _repository.Setup(r => r.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(userId, request);

        Assert.Equal("New Project", result.Name);
        Assert.Equal("https://new.example.com", result.ProductUrl);
        Assert.Equal("Smoke tests only.", result.TestingStrategy);

        _repository.Verify(r => r.CreateAsync(
            It.Is<Project>(p =>
                p.UserId == userId &&
                p.Name == "New Project" &&
                p.ProductUrl == "https://new.example.com" &&
                p.TestingStrategy == "Smoke tests only." &&
                p.IsDeleted == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ReturnsSuccess_WhenProjectBelongsToUser()
    {
        var existing = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var request = new UpdateProjectRequest("Updated Name", "https://updated.example.com", "E2E focus.");
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal("Updated Name", dto!.Name);
        Assert.Equal("https://updated.example.com", dto.ProductUrl);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var otherUserProject = MakeProject(userId: "other-user");
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserProject);

        var request = new UpdateProjectRequest("X", "https://x.com", "Y");
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        Assert.Null(dto);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        _repository.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-999", new UpdateProjectRequest("X", "https://x.com", "Y"));

        Assert.Equal(ProjectOperationResult.NotFound, result);
        Assert.Null(dto);
    }

    // ─── RequestTimeoutSeconds — feature 0022 ─────────────────────────────────

    [Fact]
    public async Task CreateAsync_DefaultsRequestTimeoutSeconds_To30_WhenNotExplicitlySupplied()
    {
        // The default value on the DTO record is 30 (ProjectConstants.RequestTimeoutDefaultSeconds).
        // When the DTO is constructed with defaults, CreateAsync must write 30 to the entity.
        var userId = "user-1";
        var request = new CreateProjectRequest("My App", "https://app.example.com", "Smoke tests.");
        // request.RequestTimeoutSeconds == 30 by default via the record default parameter

        Project? captured = null;
        _repository.Setup(r => r.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(userId, request);

        Assert.NotNull(captured);
        Assert.Equal(ProjectConstants.RequestTimeoutDefaultSeconds, captured!.RequestTimeoutSeconds);
        Assert.Equal(ProjectConstants.RequestTimeoutDefaultSeconds, result.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task CreateAsync_PersistsExplicitRequestTimeoutSeconds()
    {
        var userId = "user-1";
        var request = new CreateProjectRequest("My App", "https://app.example.com", "Smoke tests.", null, RequestTimeoutSeconds: 60);

        Project? captured = null;
        _repository.Setup(r => r.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var result = await _sut.CreateAsync(userId, request);

        Assert.NotNull(captured);
        Assert.Equal(60, captured!.RequestTimeoutSeconds);
        Assert.Equal(60, result.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task UpdateAsync_PersistsRequestTimeoutSeconds_FromRequest()
    {
        var existing = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var request = new UpdateProjectRequest("Updated Name", "https://updated.example.com", "E2E focus.", null, RequestTimeoutSeconds: 90);
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(90, dto!.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task ToDto_MapsRequestTimeoutSeconds_FromEntity()
    {
        var project = MakeProject();
        project.RequestTimeoutSeconds = 45;
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _sut.GetAsync("user-1", "proj-1");

        Assert.NotNull(result);
        Assert.Equal(45, result!.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task ToDto_Returns30_WhenEntityRequestTimeoutSeconds_IsZero()
    {
        // Existing documents that lack the field will deserialise RequestTimeoutSeconds as 0.
        // ToDto must return the default (30) in that case (AC-009).
        var project = MakeProject();
        project.RequestTimeoutSeconds = 0;
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _sut.GetAsync("user-1", "proj-1");

        Assert.NotNull(result);
        Assert.Equal(ProjectConstants.RequestTimeoutDefaultSeconds, result!.RequestTimeoutSeconds);
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_SetsIsDeleted_AndReturnsSuccess()
    {
        var existing = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var result = await _sut.DeleteAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        _repository.Verify(r => r.UpdateAsync(
            It.Is<Project>(p => p.IsDeleted == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var otherUserProject = MakeProject(userId: "other-user");
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserProject);

        var result = await _sut.DeleteAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        _repository.Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var result = await _sut.DeleteAsync("user-1", "proj-999");

        Assert.Equal(ProjectOperationResult.NotFound, result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
