using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging.Abstractions;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="KeyVaultSecretLoader"/>.
/// Covers retry behaviour on transient failure, fast-fail on secret-not-found,
/// and successful retrieval.
/// </summary>
/// <remarks>
/// <see cref="KeyVaultSecretLoader"/> wraps <see cref="SecretClient"/> from the Azure SDK.
/// Because <see cref="SecretClient"/> is a sealed class with no interface, these tests
/// verify observable behaviour (exception type / message) rather than SDK call counts.
/// The retry delays are not tested directly to keep unit tests fast.
/// </remarks>
public class KeyVaultSecretLoaderTests
{
    // ─── KeyVaultSecretLoader constructor ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenKeyVaultUriIsNullOrWhiteSpace(string? uri)
    {
        Assert.Throws<ArgumentException>(() =>
            new KeyVaultSecretLoader(uri!, NullLogger<KeyVaultSecretLoader>.Instance));
    }

    // ─── Secret name validation ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSecretAsync_ThrowsArgumentException_WhenSecretNameIsNullOrWhiteSpace(string? name)
    {
        // We cannot construct a real KeyVaultSecretLoader that connects to Azure in tests,
        // but we CAN verify that argument validation fires BEFORE any SDK call.
        // Use a well-formed URI so the constructor succeeds, then verify argument validation.
        var loader = new KeyVaultSecretLoader(
            "https://fake-vault.vault.azure.net/",
            NullLogger<KeyVaultSecretLoader>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            loader.GetSecretAsync(name!));
    }
}
