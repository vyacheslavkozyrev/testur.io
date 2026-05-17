namespace Testurio.Core.Interfaces;

public interface ISecretResolver
{
    /// <summary>
    /// Resolves a secret by its reference name. Throws <see cref="System.InvalidOperationException"/>
    /// (or a provider-specific exception) when the secret does not exist or is empty/revoked.
    /// Callers must not assume an empty-string return is valid.
    /// </summary>
    Task<string> ResolveAsync(string secretRef, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="secretValue"/> under <paramref name="secretName"/>.
    /// Storing <see cref="string.Empty"/> is the accepted soft-delete (revocation) pattern:
    /// it creates a new version of the secret with a blank value, which <see cref="ResolveAsync"/>
    /// callers must treat as absent by validating the resolved value is non-empty before use.
    /// </summary>
    Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
}
