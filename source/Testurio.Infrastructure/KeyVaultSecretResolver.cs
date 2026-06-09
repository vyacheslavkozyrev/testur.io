using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure;

/// <summary>
/// Production implementation: resolves and stores project secrets via Azure Key Vault.
/// <para>
/// When constructed with an <see cref="IKeyVaultSecretLoader"/>, <see cref="ResolveAsync"/>
/// delegates to the loader so that a single <see cref="SecretClient"/> (and a single
/// Managed Identity token acquisition) services both startup-time application secrets
/// (via <c>IKeyVaultSecretLoader</c>) and runtime project-credential lookups.
/// </para>
/// <para>
/// <see cref="StoreAsync"/> always creates its own <see cref="SecretClient"/> directly,
/// because <see cref="IKeyVaultSecretLoader"/> is a read-only abstraction.
/// </para>
/// </summary>
public sealed class KeyVaultSecretResolver : ISecretResolver
{
    private readonly IKeyVaultSecretLoader? _loader;
    private readonly SecretClient _writeClient;

    /// <summary>
    /// Preferred constructor — delegates reads to <paramref name="loader"/> to avoid
    /// a second independent <see cref="SecretClient"/> at runtime.
    /// </summary>
    public KeyVaultSecretResolver(IKeyVaultSecretLoader loader, string keyVaultUri)
    {
        _loader = loader;
        _writeClient = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
    }

    /// <summary>
    /// Legacy constructor kept for backwards compatibility. Creates its own
    /// <see cref="SecretClient"/>; prefer <see cref="KeyVaultSecretResolver(IKeyVaultSecretLoader, string)"/>.
    /// </summary>
    public KeyVaultSecretResolver(string keyVaultUri)
    {
        _writeClient = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
    }

    public async Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default)
    {
        if (_loader is not null)
            return await _loader.GetSecretAsync(secretRef, cancellationToken);

        var response = await _writeClient.GetSecretAsync(secretRef, cancellationToken: cancellationToken);
        return response.Value.Value;
    }

    public async Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        await _writeClient.SetSecretAsync(secretName, secretValue, cancellationToken);
    }
}
