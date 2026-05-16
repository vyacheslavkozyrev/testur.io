using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.Api.Services;

public partial class ProjectAccessService : IProjectAccessService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<ProjectAccessService> _logger;

    public ProjectAccessService(
        IProjectRepository projectRepository,
        ISecretResolver secretResolver,
        ILogger<ProjectAccessService> logger)
    {
        _projectRepository = projectRepository;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    public async Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> GetAsync(
        string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        if (project.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        return (ProjectOperationResult.Success, BuildDto(project));
    }

    public async Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> UpdateAsync(
        string userId, string projectId, UpdateProjectAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        if (project.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        // AC-040: Capture old URIs before any mutations. New Key Vault writes happen first;
        // if they throw, Cosmos is never touched and the old config remains intact.
        var oldPassUri  = project.BasicAuthPassSecretUri;
        var oldTokenUri = project.HeaderTokenSecretUri;

        project.AccessMode = request.AccessMode;
        project.BasicAuthUser = null;
        project.BasicAuthPassSecretUri = null;
        project.HeaderTokenName = null;
        project.HeaderTokenSecretUri = null;

        if (request.AccessMode == AccessMode.BasicAuth)
        {
            project.BasicAuthUser = request.BasicAuthUser;

            var passSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.BasicAuthPass);

            if (!string.IsNullOrWhiteSpace(request.BasicAuthPass))
            {
                // New password provided — write to Key Vault.
                await _secretResolver.StoreAsync(passSecretName, request.BasicAuthPass, cancellationToken);
                project.BasicAuthPassSecretUri = passSecretName;
                // If the old URI is the same secret name, it was just overwritten with a new value
                // — prevent ClearSecretsAsync from wiping the fresh credential.
                if (oldPassUri == passSecretName) oldPassUri = null;
            }
            else
            {
                // No new password — preserve the existing Key Vault secret.
                project.BasicAuthPassSecretUri = oldPassUri;
                oldPassUri = null;
            }

            LogBasicAuthConfigured(_logger, projectId, userId);
        }
        else if (request.AccessMode == AccessMode.HeaderToken)
        {
            project.HeaderTokenName = request.HeaderTokenName;

            var tokenSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.HeaderTokenValue);

            if (!string.IsNullOrWhiteSpace(request.HeaderTokenValue))
            {
                await _secretResolver.StoreAsync(tokenSecretName, request.HeaderTokenValue, cancellationToken);
                project.HeaderTokenSecretUri = tokenSecretName;
                if (oldTokenUri == tokenSecretName) oldTokenUri = null;
            }
            else
            {
                project.HeaderTokenSecretUri = oldTokenUri;
                oldTokenUri = null;
            }

            LogHeaderTokenConfigured(_logger, projectId, userId);
        }
        else
        {
            LogIpAllowlistConfigured(_logger, projectId, userId);
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);

        // Best-effort: stale secrets with known names are harmless once the Cosmos URIs are removed.
        try
        {
            await ClearSecretsAsync(oldPassUri, oldTokenUri, cancellationToken);
        }
        catch (Exception ex)
        {
            LogSecretCleanupFailed(_logger, projectId, ex.Message);
        }

        return (ProjectOperationResult.Success, BuildDto(updated));
    }

    private static ProjectAccessDto BuildDto(Project project) => new(
        ProjectId: project.Id,
        AccessMode: project.AccessMode,
        BasicAuthUser: project.AccessMode == AccessMode.BasicAuth ? project.BasicAuthUser : null,
        HeaderTokenName: project.AccessMode == AccessMode.HeaderToken ? project.HeaderTokenName : null);

    private async Task ClearSecretsAsync(
        string? passUri, string? tokenUri, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(passUri))
            await _secretResolver.StoreAsync(passUri, string.Empty, cancellationToken);

        if (!string.IsNullOrWhiteSpace(tokenUri))
            await _secretResolver.StoreAsync(tokenUri, string.Empty, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} access mode set to IpAllowlist by user {UserId}")]
    private static partial void LogIpAllowlistConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} access mode set to BasicAuth by user {UserId}")]
    private static partial void LogBasicAuthConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} access mode set to HeaderToken by user {UserId}")]
    private static partial void LogHeaderTokenConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Project {ProjectId} — best-effort secret cleanup failed: {ErrorMessage}")]
    private static partial void LogSecretCleanupFailed(ILogger logger, string projectId, string errorMessage);
}
