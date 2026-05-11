using System.Collections.Concurrent;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure;

// Dev/test implementation — stores secrets in memory and resolves them by name.
// Production: replace with KeyVaultSecretResolver that calls SecretClient.GetSecretAsync.
public sealed class PassthroughSecretResolver : ISecretResolver
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(secretRef, out var value) ? value : secretRef);

    public Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        _store[secretName] = secretValue;
        return Task.CompletedTask;
    }
}
