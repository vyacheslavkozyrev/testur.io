using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure;

// Production implementation — resolves a Key Vault secret reference via SecretClient.
// Wire up in a future feature once Azure.Security.KeyVault.Secrets is integrated.
public sealed class KeyVaultSecretResolver : ISecretResolver
{
    public Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "KeyVaultSecretResolver is not yet implemented. " +
            "Integrate Azure.Security.KeyVault.Secrets and replace this stub before deploying to production.");
}
