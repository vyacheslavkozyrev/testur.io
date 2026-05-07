using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.KeyVault;

/// <summary>
/// Resolves project credentials (e.g. Bearer token) from Key Vault secret references.
/// Secret values are never cached in memory or logged — they are resolved on each call
/// and passed directly to the caller.
/// </summary>
public sealed class KeyVaultCredentialClient
{
    private readonly ISecretResolver _secretResolver;

    public KeyVaultCredentialClient(ISecretResolver secretResolver)
    {
        _secretResolver = secretResolver;
    }

    /// <summary>
    /// Resolves the Bearer token for a project.
    /// Returns null if <paramref name="secretRef"/> is null or empty (AC-007 — no auth header).
    /// The resolved token value is never written to logs.
    /// </summary>
    public async Task<string?> ResolveBearerTokenAsync(string? secretRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            return null;

        return await _secretResolver.ResolveAsync(secretRef, cancellationToken);
    }
}
