namespace Testurio.Core.Interfaces;

public interface ISecretResolver
{
    Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default);
}
