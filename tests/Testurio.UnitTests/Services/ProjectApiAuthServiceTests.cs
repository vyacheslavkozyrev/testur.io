using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Services;

public class ProjectApiAuthServiceTests
{
    private readonly Mock<IProjectRepository> _repository = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly ProjectApiAuthService _sut;

    public ProjectApiAuthServiceTests()
    {
        _sut = new ProjectApiAuthService(
            _repository.Object,
            _secretResolver.Object,
            NullLogger<ProjectApiAuthService>.Instance);
    }

    private static Project MakeProject(string userId = "user-1", string projectId = "proj-1") => new()
    {
        Id = projectId,
        UserId = userId,
        Name = "My App",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Focus on API contracts.",
        ApiAuthMethod = ApiAuthMethod.None,
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
        var other = MakeProject(userId: "other-user");
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetAsync_ReturnsNoneDto_WhenMethodIsNone()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var (result, dto) = await _sut.GetAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal("none", dto.ApiAuthMethod);
        Assert.Null(dto.ApiAuthBearerTokenConfigured);
        Assert.Null(dto.ApiAuthApiKeyName);
        Assert.Null(dto.ApiAuthBasicUsername);
        _secretResolver.Verify(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── UpdateAsync — None ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_SetsNone_AndClearsPreviousBearerSecret()
    {
        var project = MakeProject();
        project.ApiAuthMethod = ApiAuthMethod.Bearer;
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "none" };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal("none", dto.ApiAuthMethod);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-bearer-token", string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── UpdateAsync — Bearer ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_StoresBearerToken_AndReturnsBearerTokenConfiguredTrue()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "bearer", ApiAuthBearerToken = "tok-secret" };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal("bearer", dto.ApiAuthMethod);
        Assert.True(dto.ApiAuthBearerTokenConfigured);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-bearer-token", "tok-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── UpdateAsync — ApiKey ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_StoresApiKeyValue_AndReturnsKeyNameAndPlacementInDto()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest
        {
            ApiAuthMethod       = "api_key",
            ApiAuthApiKeyName   = "X-Api-Key",
            ApiAuthApiKeyPlacement = "header",
            ApiAuthApiKeyValue  = "key-secret",
        };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal("api_key", dto.ApiAuthMethod);
        Assert.Equal("X-Api-Key", dto.ApiAuthApiKeyName);
        Assert.Equal("header", dto.ApiAuthApiKeyPlacement);
        Assert.True(dto.ApiAuthApiKeyValueConfigured);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-api-key-value", "key-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── UpdateAsync — Basic ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_StoresBasicPassword_AndReturnsUsernameInDto()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest
        {
            ApiAuthMethod       = "basic",
            ApiAuthBasicUsername = "api-user",
            ApiAuthBasicPassword = "api-pass",
        };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Equal("basic", dto.ApiAuthMethod);
        Assert.Equal("api-user", dto.ApiAuthBasicUsername);
        Assert.True(dto.ApiAuthBasicPasswordConfigured);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-basic-password", "api-pass", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── UpdateAsync — mode switch clears obsolete secrets ───────────────────

    [Fact]
    public async Task UpdateAsync_SwitchingFromBearerToNone_ClearsBearerSecret()
    {
        var project = MakeProject();
        project.ApiAuthMethod = ApiAuthMethod.Bearer;
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "none" };
        await _sut.UpdateAsync("user-1", "proj-1", request);

        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-bearer-token", string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SwitchingBearerToBearerDoesNotWipeFreshlyStoredToken()
    {
        var project = MakeProject();
        project.ApiAuthMethod = ApiAuthMethod.Bearer;
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "bearer", ApiAuthBearerToken = "new-tok" };
        await _sut.UpdateAsync("user-1", "proj-1", request);

        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-bearer-token", "new-tok", It.IsAny<CancellationToken>()), Times.Once);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-bearer-token", string.Empty, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_SwitchingFromApiKeyToNone_ClearsApiKeyValueSecret()
    {
        var project = MakeProject();
        project.ApiAuthMethod = ApiAuthMethod.ApiKey;
        project.ApiAuthApiKeyName = "X-Api-Key";
        project.ApiAuthApiKeyPlacement = ApiAuthApiKeyPlacement.Header;
        project.ApiAuthApiKeyValueSecretUri = "projects--proj-1--api-auth-api-key-value";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "none" };
        await _sut.UpdateAsync("user-1", "proj-1", request);

        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-api-key-value", string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SwitchingFromBasicToNone_ClearsBasicPasswordSecret()
    {
        var project = MakeProject();
        project.ApiAuthMethod = ApiAuthMethod.Basic;
        project.ApiAuthBasicUsername = "api-user";
        project.ApiAuthBasicPasswordSecretUri = "projects--proj-1--api-auth-basic-password";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "none" };
        await _sut.UpdateAsync("user-1", "proj-1", request);

        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-basic-password", string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_BearerToBearerWithFailingStore_DoesNotUpdateCosmos_AndDoesNotAttemptCleanup()
    {
        var project = MakeProject();
        project.ApiAuthMethod = ApiAuthMethod.Bearer;
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unavailable"));

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "bearer", ApiAuthBearerToken = "new-tok" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateAsync("user-1", "proj-1", request));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
        _secretResolver.Verify(s => s.StoreAsync(
            "projects--proj-1--api-auth-bearer-token", string.Empty, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── UpdateAsync — ownership / not found ─────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var other = MakeProject(userId: "other-user");
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "none" };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        Assert.Null(dto);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "none" };
        var (result, dto) = await _sut.UpdateAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.NotFound, result);
        Assert.Null(dto);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotUpdateCosmos_WhenKeyVaultWriteFails()
    {
        var project = MakeProject();
        _repository.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _secretResolver.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unavailable"));

        var request = new UpdateProjectApiAuthRequest { ApiAuthMethod = "bearer", ApiAuthBearerToken = "tok" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateAsync("user-1", "proj-1", request));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
