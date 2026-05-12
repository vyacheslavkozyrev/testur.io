namespace Testurio.Core.Interfaces;

public interface ISecretResolver
{
    Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default);
    Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
}
