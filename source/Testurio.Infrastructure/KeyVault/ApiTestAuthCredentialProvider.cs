using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.KeyVault;

/// <summary>
/// Resolves API test authentication credentials for a project from Azure Key Vault.
/// Secret values are fetched at call time and never cached beyond a single pipeline run.
/// </summary>
public sealed class ApiTestAuthCredentialProvider : IApiTestAuthCredentialProvider
{
    private readonly ISecretResolver _secretResolver;

    public ApiTestAuthCredentialProvider(ISecretResolver secretResolver)
    {
        _secretResolver = secretResolver;
    }

    public async Task<ApiTestAuthCredentials> ResolveAsync(Project project, CancellationToken ct = default)
    {
        try
        {
            return (project.ApiAuthMethod) switch
            {
                ApiAuthMethod.None => new ApiTestAuthCredentials.None(),

                ApiAuthMethod.Bearer => await ResolveBearerAsync(project, ct),

                ApiAuthMethod.ApiKey => await ResolveApiKeyAsync(project, ct),

                ApiAuthMethod.Basic => await ResolveBasicAsync(project, ct),

                _ => throw new CredentialRetrievalException($"Unknown API auth method: {project.ApiAuthMethod}"),
            };
        }
        catch (CredentialRetrievalException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CredentialRetrievalException(
                $"Failed to retrieve API auth credentials for project {project.Id}: {ex.Message}", ex);
        }
    }

    private async Task<ApiTestAuthCredentials.Bearer> ResolveBearerAsync(Project project, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(project.ApiAuthBearerTokenSecretUri))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for Bearer auth but ApiAuthBearerTokenSecretUri is not set.");

        var token = await _secretResolver.ResolveAsync(project.ApiAuthBearerTokenSecretUri, ct);
        return new ApiTestAuthCredentials.Bearer(token);
    }

    private async Task<ApiTestAuthCredentials.ApiKey> ResolveApiKeyAsync(Project project, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(project.ApiAuthApiKeyName))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for ApiKey auth but ApiAuthApiKeyName is not set.");

        if (string.IsNullOrWhiteSpace(project.ApiAuthApiKeyValueSecretUri))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for ApiKey auth but ApiAuthApiKeyValueSecretUri is not set.");

        var value = await _secretResolver.ResolveAsync(project.ApiAuthApiKeyValueSecretUri, ct);
        return new ApiTestAuthCredentials.ApiKey(project.ApiAuthApiKeyName, project.ApiAuthApiKeyPlacement, value);
    }

    private async Task<ApiTestAuthCredentials.Basic> ResolveBasicAsync(Project project, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(project.ApiAuthBasicUsername))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for Basic auth but ApiAuthBasicUsername is not set.");

        if (string.IsNullOrWhiteSpace(project.ApiAuthBasicPasswordSecretUri))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for Basic auth but ApiAuthBasicPasswordSecretUri is not set.");

        var password = await _secretResolver.ResolveAsync(project.ApiAuthBasicPasswordSecretUri, ct);
        return new ApiTestAuthCredentials.Basic(project.ApiAuthBasicUsername, password);
    }
}
