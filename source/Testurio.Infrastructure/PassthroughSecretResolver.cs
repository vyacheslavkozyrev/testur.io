using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure;

// Dev/test implementation — returns the stored value as-is.
// Production: replace with KeyVaultSecretResolver that calls SecretClient.GetSecretAsync.
public sealed class PassthroughSecretResolver : ISecretResolver
{
    public Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default)
        => Task.FromResult(secretRef);
}
