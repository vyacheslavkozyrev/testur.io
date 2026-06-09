namespace Testurio.Core.Interfaces;

/// <summary>
/// Loads named secrets from Azure Key Vault at application startup.
/// Used by <c>Add*Secrets</c> DI extension methods in <c>Testurio.Infrastructure</c>
/// to populate <c>*Secrets</c> singleton objects before the rest of the container is built.
/// </summary>
/// <remarks>
/// This interface is intentionally separate from <see cref="ISecretResolver"/>, which handles
/// per-project credential URIs at runtime. <c>IKeyVaultSecretLoader</c> is concerned only with
/// global application secrets resolved once at startup.
/// </remarks>
public interface IKeyVaultSecretLoader
{
    /// <summary>
    /// Returns the value of the named secret.
    /// </summary>
    /// <param name="secretName">The Key Vault secret name, e.g. <c>cosmos-connection-string</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret value as a plain string.</returns>
    /// <exception cref="Azure.RequestFailedException">
    /// Thrown when the secret does not exist or Key Vault is unreachable after all retries.
    /// </exception>
    Task<string> GetSecretAsync(string secretName, CancellationToken ct = default);
}
