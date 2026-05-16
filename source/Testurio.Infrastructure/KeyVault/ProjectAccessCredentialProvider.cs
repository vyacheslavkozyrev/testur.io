using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.KeyVault;

/// <summary>
/// Resolves access credentials for a project's staging environment from Azure Key Vault.
/// Secret values are fetched at call time and never cached beyond a single pipeline run.
/// </summary>
public sealed class ProjectAccessCredentialProvider : IProjectAccessCredentialProvider
{
    private readonly ISecretResolver _secretResolver;

    public ProjectAccessCredentialProvider(ISecretResolver secretResolver)
    {
        _secretResolver = secretResolver;
    }

    public async Task<ProjectAccessCredentials> ResolveAsync(Project project, CancellationToken cancellationToken = default)
    {
        try
        {
            return project.AccessMode switch
            {
                AccessMode.IpAllowlist => new ProjectAccessCredentials.IpAllowlist(),

                AccessMode.BasicAuth => await ResolveBasicAuthAsync(project, cancellationToken),

                AccessMode.HeaderToken => await ResolveHeaderTokenAsync(project, cancellationToken),

                _ => throw new CredentialRetrievalException($"Unknown access mode: {project.AccessMode}"),
            };
        }
        catch (CredentialRetrievalException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CredentialRetrievalException(
                $"Failed to retrieve access credentials for project {project.Id}: {ex.Message}", ex);
        }
    }

    private async Task<ProjectAccessCredentials.BasicAuth> ResolveBasicAuthAsync(
        Project project, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.BasicAuthUserSecretUri))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for BasicAuth but BasicAuthUserSecretUri is not set.");

        if (string.IsNullOrWhiteSpace(project.BasicAuthPassSecretUri))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for BasicAuth but BasicAuthPassSecretUri is not set.");

        var username = await _secretResolver.ResolveAsync(project.BasicAuthUserSecretUri, cancellationToken);
        var password = await _secretResolver.ResolveAsync(project.BasicAuthPassSecretUri, cancellationToken);
        return new ProjectAccessCredentials.BasicAuth(username, password);
    }

    private async Task<ProjectAccessCredentials.HeaderToken> ResolveHeaderTokenAsync(
        Project project, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.HeaderTokenName))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for HeaderToken but HeaderTokenName is not set.");

        if (string.IsNullOrWhiteSpace(project.HeaderTokenSecretUri))
            throw new CredentialRetrievalException(
                $"Project {project.Id} is configured for HeaderToken but HeaderTokenSecretUri is not set.");

        var headerValue = await _secretResolver.ResolveAsync(project.HeaderTokenSecretUri, cancellationToken);
        return new ProjectAccessCredentials.HeaderToken(project.HeaderTokenName, headerValue);
    }
}
