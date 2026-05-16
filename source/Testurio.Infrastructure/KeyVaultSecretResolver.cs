using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure;

/// <summary>
/// Production implementation: resolves and stores project secrets via Azure Key Vault.
/// Uses DefaultAzureCredential (Managed Identity in Azure, az login / env vars locally).
/// </summary>
public sealed class KeyVaultSecretResolver : ISecretResolver
{
    private readonly SecretClient _client;

    public KeyVaultSecretResolver(string keyVaultUri)
    {
        _client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
    }

    public async Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetSecretAsync(secretRef, cancellationToken: cancellationToken);
        return response.Value.Value;
    }

    public async Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        await _client.SetSecretAsync(secretName, secretValue, cancellationToken);
    }
}
