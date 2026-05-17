using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.UnitTests.Services;

public class ApiTestAuthCredentialProviderTests
{
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly ApiTestAuthCredentialProvider _sut;

    public ApiTestAuthCredentialProviderTests()
    {
        _sut = new ApiTestAuthCredentialProvider(_secretResolver.Object);
    }

    private static Project MakeProject(ApiAuthMethod method = ApiAuthMethod.None) => new()
    {
        Id = "proj-1",
        UserId = "user-1",
        Name = "My App",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API tests.",
        ApiAuthMethod = method,
    };

    // ─── None ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsNone_AndMakesNoKeyVaultCall()
    {
        var project = MakeProject(ApiAuthMethod.None);

        var credentials = await _sut.ResolveAsync(project);

        Assert.IsType<ApiTestAuthCredentials.None>(credentials);
        _secretResolver.Verify(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Bearer ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsBearer_WithResolvedToken()
    {
        var project = MakeProject(ApiAuthMethod.Bearer);
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--api-auth-bearer-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tok-abc123");

        var credentials = await _sut.ResolveAsync(project);

        var bearer = Assert.IsType<ApiTestAuthCredentials.Bearer>(credentials);
        Assert.Equal("tok-abc123", bearer.Token);
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenBearerSecretUriMissing()
    {
        var project = MakeProject(ApiAuthMethod.Bearer);
        // ApiAuthBearerTokenSecretUri is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    // ─── ApiKey ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsApiKey_WithHeaderPlacement()
    {
        var project = MakeProject(ApiAuthMethod.ApiKey);
        project.ApiAuthApiKeyName = "X-Api-Key";
        project.ApiAuthApiKeyPlacement = ApiAuthApiKeyPlacement.Header;
        project.ApiAuthApiKeyValueSecretUri = "projects--proj-1--api-auth-api-key-value";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--api-auth-api-key-value", It.IsAny<CancellationToken>()))
            .ReturnsAsync("my-api-key");

        var credentials = await _sut.ResolveAsync(project);

        var apiKey = Assert.IsType<ApiTestAuthCredentials.ApiKey>(credentials);
        Assert.Equal("X-Api-Key", apiKey.Name);
        Assert.Equal(ApiAuthApiKeyPlacement.Header, apiKey.Placement);
        Assert.Equal("my-api-key", apiKey.Value);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsApiKey_WithQueryPlacement()
    {
        var project = MakeProject(ApiAuthMethod.ApiKey);
        project.ApiAuthApiKeyName = "api_key";
        project.ApiAuthApiKeyPlacement = ApiAuthApiKeyPlacement.Query;
        project.ApiAuthApiKeyValueSecretUri = "projects--proj-1--api-auth-api-key-value";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--api-auth-api-key-value", It.IsAny<CancellationToken>()))
            .ReturnsAsync("qry-secret");

        var credentials = await _sut.ResolveAsync(project);

        var apiKey = Assert.IsType<ApiTestAuthCredentials.ApiKey>(credentials);
        Assert.Equal(ApiAuthApiKeyPlacement.Query, apiKey.Placement);
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenApiKeyNameMissing()
    {
        var project = MakeProject(ApiAuthMethod.ApiKey);
        project.ApiAuthApiKeyValueSecretUri = "projects--proj-1--api-auth-api-key-value";
        // ApiAuthApiKeyName is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenApiKeySecretUriMissing()
    {
        var project = MakeProject(ApiAuthMethod.ApiKey);
        project.ApiAuthApiKeyName = "X-Api-Key";
        // ApiAuthApiKeyValueSecretUri is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    // ─── Basic ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsBasic_WithUsernameAndPassword()
    {
        var project = MakeProject(ApiAuthMethod.Basic);
        project.ApiAuthBasicUsername = "api-user";
        project.ApiAuthBasicPasswordSecretUri = "projects--proj-1--api-auth-basic-password";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--api-auth-basic-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync("api-pass");

        var credentials = await _sut.ResolveAsync(project);

        var basic = Assert.IsType<ApiTestAuthCredentials.Basic>(credentials);
        Assert.Equal("api-user", basic.Username);
        Assert.Equal("api-pass", basic.Password);
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenBasicUsernameMissing()
    {
        var project = MakeProject(ApiAuthMethod.Basic);
        project.ApiAuthBasicPasswordSecretUri = "projects--proj-1--api-auth-basic-password";
        // ApiAuthBasicUsername is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenBasicPasswordSecretUriMissing()
    {
        var project = MakeProject(ApiAuthMethod.Basic);
        project.ApiAuthBasicUsername = "api-user";
        // ApiAuthBasicPasswordSecretUri is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    // ─── Empty/revoked secrets ────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenBearerTokenResolvedEmpty()
    {
        var project = MakeProject(ApiAuthMethod.Bearer);
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--api-auth-bearer-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    // ─── Key Vault unreachable ─────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WrapsKeyVaultException_AsCredentialRetrievalException()
    {
        var project = MakeProject(ApiAuthMethod.Bearer);
        project.ApiAuthBearerTokenSecretUri = "projects--proj-1--api-auth-bearer-token";

        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unreachable"));

        var ex = await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}
