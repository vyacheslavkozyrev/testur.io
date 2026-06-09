using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.KeyVault;

/// <summary>
/// Production implementation of <see cref="IKeyVaultSecretLoader"/> that reads secrets from
/// Azure Key Vault using <see cref="DefaultAzureCredential"/> (Managed Identity in Azure,
/// developer credential locally when explicitly configured).
/// Retries up to 3 times with exponential back-off (1s, 2s, 4s) to cover transient IMDS
/// cold-start latency on Azure Container Apps.
/// </summary>
public sealed partial class KeyVaultSecretLoader : IKeyVaultSecretLoader
{
    private readonly SecretClient _client;
    private readonly ILogger<KeyVaultSecretLoader> _logger;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    public KeyVaultSecretLoader(string keyVaultUri, ILogger<KeyVaultSecretLoader> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyVaultUri);
        _client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        Exception? lastException = null;

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                var response = await _client.GetSecretAsync(secretName, cancellationToken: ct);
                LogSecretLoaded(secretName);
                return response.Value.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Secret not found — do not retry; fail fast immediately.
                LogSecretNotFound(secretName);
                throw;
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                lastException = ex;
                var delay = RetryDelays[attempt];
                LogRetrying(secretName, attempt + 1, (int)delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        LogAllRetriesExhausted(secretName);
        throw new InvalidOperationException(
            $"Failed to load secret '{secretName}' from Key Vault after {RetryDelays.Length + 1} attempts.",
            lastException);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded Key Vault secret '{SecretName}'.")]
    private partial void LogSecretLoaded(string secretName);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Key Vault secret '{SecretName}' was not found (HTTP 404). Verify the secret exists in the vault.")]
    private partial void LogSecretNotFound(string secretName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Key Vault secret '{SecretName}' load attempt {Attempt} failed (retry in {DelaySeconds}s): {Message}")]
    private partial void LogRetrying(string secretName, int attempt, int delaySeconds, string message);

    [LoggerMessage(Level = LogLevel.Critical, Message = "All retry attempts exhausted loading Key Vault secret '{SecretName}'.")]
    private partial void LogAllRetriesExhausted(string secretName);
}
