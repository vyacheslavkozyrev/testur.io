using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.UnitTests.Services;

public class ProjectAccessCredentialProviderTests
{
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly ProjectAccessCredentialProvider _sut;

    public ProjectAccessCredentialProviderTests()
    {
        _sut = new ProjectAccessCredentialProvider(_secretResolver.Object);
    }

    private static Project MakeProject(AccessMode mode = AccessMode.IpAllowlist) => new()
    {
        Id = "proj-1",
        UserId = "user-1",
        Name = "My App",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API tests.",
        AccessMode = mode,
    };

    // ─── IpAllowlist ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsIpAllowlist_AndMakesNoKeyVaultCall()
    {
        var project = MakeProject(AccessMode.IpAllowlist);

        var credentials = await _sut.ResolveAsync(project);

        Assert.IsType<ProjectAccessCredentials.IpAllowlist>(credentials);
        _secretResolver.Verify(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── BasicAuth ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsBasicAuth_WithResolvedCredentials()
    {
        var project = MakeProject(AccessMode.BasicAuth);
        project.BasicAuthUserSecretUri = "projects--proj-1--basic-auth-user";
        project.BasicAuthPassSecretUri = "projects--proj-1--basic-auth-pass";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--basic-auth-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("admin");
        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--basic-auth-pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync("s3cret");

        var credentials = await _sut.ResolveAsync(project);

        var basicAuth = Assert.IsType<ProjectAccessCredentials.BasicAuth>(credentials);
        Assert.Equal("admin", basicAuth.Username);
        Assert.Equal("s3cret", basicAuth.Password);
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenBasicAuthUserUriMissing()
    {
        var project = MakeProject(AccessMode.BasicAuth);
        project.BasicAuthPassSecretUri = "projects--proj-1--basic-auth-pass";
        // BasicAuthUserSecretUri is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenBasicAuthPassUriMissing()
    {
        var project = MakeProject(AccessMode.BasicAuth);
        project.BasicAuthUserSecretUri = "projects--proj-1--basic-auth-user";
        // BasicAuthPassSecretUri is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    // ─── HeaderToken ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsHeaderToken_WithResolvedValue()
    {
        var project = MakeProject(AccessMode.HeaderToken);
        project.HeaderTokenName = "X-Testurio-Token";
        project.HeaderTokenSecretUri = "projects--proj-1--header-token-value";

        _secretResolver.Setup(s => s.ResolveAsync("projects--proj-1--header-token-value", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tok-abc123");

        var credentials = await _sut.ResolveAsync(project);

        var headerToken = Assert.IsType<ProjectAccessCredentials.HeaderToken>(credentials);
        Assert.Equal("X-Testurio-Token", headerToken.HeaderName);
        Assert.Equal("tok-abc123", headerToken.HeaderValue);
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenHeaderTokenNameMissing()
    {
        var project = MakeProject(AccessMode.HeaderToken);
        project.HeaderTokenSecretUri = "projects--proj-1--header-token-value";
        // HeaderTokenName is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    [Fact]
    public async Task ResolveAsync_ThrowsCredentialRetrievalException_WhenHeaderTokenSecretUriMissing()
    {
        var project = MakeProject(AccessMode.HeaderToken);
        project.HeaderTokenName = "X-Testurio-Token";
        // HeaderTokenSecretUri is null

        await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
    }

    // ─── Key Vault unreachable ─────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WrapsKeyVaultException_AsCredentialRetrievalException()
    {
        var project = MakeProject(AccessMode.BasicAuth);
        project.BasicAuthUserSecretUri = "projects--proj-1--basic-auth-user";
        project.BasicAuthPassSecretUri = "projects--proj-1--basic-auth-pass";

        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unreachable"));

        var ex = await Assert.ThrowsAsync<CredentialRetrievalException>(() => _sut.ResolveAsync(project));
        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}
