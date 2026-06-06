using Testurio.Infrastructure.KeyVault;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="NullKeyVaultSecretLoader"/>.
/// Verifies that the no-op loader always returns an empty string and never
/// calls any Azure SDK method (no Azure credential is created).
/// </summary>
public class NullKeyVaultSecretLoaderTests
{
    [Fact]
    public async Task GetSecretAsync_ReturnsEmptyString_ForAnySecretName()
    {
        var loader = new NullKeyVaultSecretLoader();
        var result = await loader.GetSecretAsync("cosmos-connection-string");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsEmptyString_ForAllKnownSecretNames()
    {
        var loader = new NullKeyVaultSecretLoader();
        string[] secretNames =
        [
            "cosmos-connection-string",
            "servicebus-connection-string",
            "blob-storage-connection-string",
            "anthropic-api-key",
            "azure-openai-api-key",
            "stripe-secret-key",
            "stripe-webhook-secret",
        ];

        foreach (var name in secretNames)
        {
            var result = await loader.GetSecretAsync(name);
            Assert.Equal(string.Empty, result);
        }
    }

    [Fact]
    public async Task GetSecretAsync_NeverThrows_RegardlessOfInput()
    {
        var loader = new NullKeyVaultSecretLoader();

        // Should not throw for empty or unusual input
        var r1 = await loader.GetSecretAsync(string.Empty);
        var r2 = await loader.GetSecretAsync("unknown-secret-xyz");

        Assert.Equal(string.Empty, r1);
        Assert.Equal(string.Empty, r2);
    }

    [Fact]
    public async Task GetSecretAsync_DoesNotBlockOnCancelledToken()
    {
        var loader = new NullKeyVaultSecretLoader();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // NullKeyVaultSecretLoader does zero I/O, so cancellation does not propagate.
        var result = await loader.GetSecretAsync("test-secret", cts.Token);
        Assert.Equal(string.Empty, result);
    }
}
