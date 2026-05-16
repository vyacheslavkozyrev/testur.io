using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Services;

public class ProjectAccessServiceTests
{
    private readonly Mock<IProjectRepository> _repository = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly ProjectAccessService _sut;

    public ProjectAccessServiceTests()
    {
        _sut = new ProjectAccessService(
            _repository.Object,
            _secretResolver.Object,
            NullLogger<ProjectAccessService>.Instance);
    }

    private static Project MakeProject(string userId = "user-1", string projectId = "proj-1") => new()
    {
        Id = projectId,
        UserId = userId,
        Name = "My App",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Focus on API contracts.",
        AccessMode = AccessMode.IpAllowlist,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    // ─── GetAsync — ownership ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.NotFound, result);
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user");
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetAsync_ReturnsAccessDto_WithIpAllowlistMode()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(AccessMode.IpAllowlist, dto.AccessMode);
        Assert.Null(dto.BasicAuthUser);
        Assert.Null(dto.HeaderTokenName);
    }

    [Fact]
    public async Task GetAsync_ResolvesAndReturnsUsername_WhenBasicAuthMode()
    {
        var project = MakeProject();
        project.AccessMode = AccessMode.BasicAuth;
        project.BasicAuthUserSecretUri = "projects--proj-1--basic-auth-user";
        project.BasicAuthPassSecretUri = "projects--proj-1--basic-auth-pass";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--basic-auth-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("testuser");

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(AccessMode.BasicAuth, dto.AccessMode);
        Assert.Equal("testuser", dto.BasicAuthUser);
        Assert.Null(dto.HeaderTokenName);
    }

    [Fact]
    public async Task GetAsync_ReturnsHeaderTokenName_WhenHeaderTokenMode()
    {
        var project = MakeProject();
        project.AccessMode = AccessMode.HeaderToken;
        project.HeaderTokenName = "X-Testurio-Token";
        project.HeaderTokenSecretUri = "projects--proj-1--header-token-value";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(AccessMode.HeaderToken, dto.AccessMode);
        Assert.Equal("X-Testurio-Token", dto.HeaderTokenName);
        Assert.Null(dto.BasicAuthUser);
    }

    // ─── UpdateAsync — mode switch, secret write/clear ────────────────────────

    [Fact]
    public async Task UpdateAsync_SetsIpAllowlist_AndClearsPreviousSecrets()
    {
        var project = MakeProject();
        project.AccessMode = AccessMode.BasicAuth;
        project.BasicAuthUserSecretUri = "projects--proj-1--basic-auth-user";
        project.BasicAuthPassSecretUri = "projects--proj-1--basic-auth-pass";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectAccessRequest { AccessMode = AccessMode.IpAllowlist };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(AccessMode.IpAllowlist, dto.AccessMode);

        // Previous secrets must be overwritten (cleared)
        _secretResolver.Verify(s => s.StoreAsync("projects--proj-1--basic-auth-user", string.Empty, It.IsAny<CancellationToken>()), Times.Once);
        _secretResolver.Verify(s => s.StoreAsync("projects--proj-1--basic-auth-pass", string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_StoresBasicAuthSecrets_AndReturnsUsernameInDto()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("admin");

        var request = new UpdateProjectAccessRequest
        {
            AccessMode = AccessMode.BasicAuth,
            BasicAuthUser = "admin",
            BasicAuthPass = "secret",
        };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(AccessMode.BasicAuth, dto.AccessMode);
        Assert.Equal("admin", dto.BasicAuthUser); // username resolved from KV for display

        // Password must never appear in the DTO
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--basic-auth-user", "admin", It.IsAny<CancellationToken>()), Times.Once);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--basic-auth-pass", "secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_StoresHeaderTokenSecret_AndReturnsHeaderNameInDto()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectAccessRequest
        {
            AccessMode = AccessMode.HeaderToken,
            HeaderTokenName = "X-Testurio-Token",
            HeaderTokenValue = "tok-secret",
        };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal(AccessMode.HeaderToken, dto.AccessMode);
        Assert.Equal("X-Testurio-Token", dto.HeaderTokenName);
        Assert.Null(dto.BasicAuthUser);

        // Token value stored in KV
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--header-token-value", "tok-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user");
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var request = new UpdateProjectAccessRequest { AccessMode = AccessMode.IpAllowlist };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        Assert.Null(dto);
    }


    [Fact]
    public async Task UpdateAsync_DoesNotUpdateCosmos_WhenKeyVaultWriteFails_AC040()
    {
        // AC-040: If the Key Vault write fails during a mode switch, the Cosmos document
        // must NOT be updated and the previous configuration must remain intact.
        var project = MakeProject();
        project.AccessMode = AccessMode.IpAllowlist;

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.GetByIdAsync("user-1", "proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Key Vault write fails
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unavailable"));

        var request = new UpdateProjectAccessRequest
        {
            AccessMode = AccessMode.BasicAuth,
            BasicAuthUser = "admin",
            BasicAuthPass = "secret",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateAsync("user-1", "proj-1", request));

        // Cosmos must NOT have been updated
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var request = new UpdateProjectAccessRequest { AccessMode = AccessMode.IpAllowlist };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.NotFound, result);
        Assert.Null(dto);
    }
}
