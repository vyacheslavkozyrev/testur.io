using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.KeyVault;

/// <summary>
/// Development / test no-op implementation of <see cref="IKeyVaultSecretLoader"/>.
/// Always returns <see cref="string.Empty"/> so that callers fall back to values from
/// local configuration (e.g. <c>.env</c> / .NET user secrets).
/// Never instantiates any Azure SDK credential — safe to use without Azure infrastructure.
/// </summary>
public sealed class NullKeyVaultSecretLoader : IKeyVaultSecretLoader
{
    /// <inheritdoc/>
    /// <remarks>Always returns <see cref="string.Empty"/>; the caller must fall back to local config.</remarks>
    public Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
        => Task.FromResult(string.Empty);
}
