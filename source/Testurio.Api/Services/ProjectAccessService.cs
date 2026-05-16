using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.Api.Services;

public interface IProjectAccessService
{
    Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> GetAsync(
        string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> UpdateAsync(
        string userId, string projectId, UpdateProjectAccessRequest request,
        CancellationToken cancellationToken = default);
}

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
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (anyProject is null)
            return (ProjectOperationResult.NotFound, null);

        if (anyProject.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        var dto = await BuildDtoAsync(project, cancellationToken);
        return (ProjectOperationResult.Success, dto);
    }

    public async Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> UpdateAsync(
        string userId, string projectId, UpdateProjectAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (anyProject is null)
            return (ProjectOperationResult.NotFound, null);

        if (anyProject.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        // AC-040: Write new secrets first. Only clear old secrets and update Cosmos AFTER the
        // new Key Vault writes succeed. If a new StoreAsync throws, the old secrets remain
        // intact and Cosmos is not touched — previous configuration stays in effect.
        var oldUserUri  = project.BasicAuthUserSecretUri;
        var oldPassUri  = project.BasicAuthPassSecretUri;
        var oldTokenUri = project.HeaderTokenSecretUri;

        project.AccessMode = request.AccessMode;
        project.BasicAuthUserSecretUri = null;
        project.BasicAuthPassSecretUri = null;
        project.HeaderTokenName = null;
        project.HeaderTokenSecretUri = null;

        if (request.AccessMode == AccessMode.BasicAuth)
        {
            var userSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.BasicAuthUser);
            var passSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.BasicAuthPass);

            // These may throw — Cosmos is not yet updated at this point.
            await _secretResolver.StoreAsync(userSecretName, request.BasicAuthUser!, cancellationToken);
            await _secretResolver.StoreAsync(passSecretName, request.BasicAuthPass!, cancellationToken);

            project.BasicAuthUserSecretUri = userSecretName;
            project.BasicAuthPassSecretUri = passSecretName;

            LogBasicAuthConfigured(_logger, projectId, userId);
        }
        else if (request.AccessMode == AccessMode.HeaderToken)
        {
            var tokenSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.HeaderTokenValue);

            // May throw — Cosmos is not yet updated at this point.
            await _secretResolver.StoreAsync(tokenSecretName, request.HeaderTokenValue!, cancellationToken);

            project.HeaderTokenName = request.HeaderTokenName;
            project.HeaderTokenSecretUri = tokenSecretName;

            LogHeaderTokenConfigured(_logger, projectId, userId);
        }
        else
        {
            LogIpAllowlistConfigured(_logger, projectId, userId);
        }

        // New secrets stored successfully — now update Cosmos and clear the old secrets.
        project.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);

        // Best-effort cleanup of old secrets (fire-and-forget errors are acceptable here:
        // stale secrets with known names are harmless once the Cosmos URIs are removed).
        await ClearSecretsAsync(oldUserUri, oldPassUri, oldTokenUri, cancellationToken);
        var dto = await BuildDtoAsync(updated, cancellationToken);
        return (ProjectOperationResult.Success, dto);
    }

    private async Task<ProjectAccessDto> BuildDtoAsync(Project project, CancellationToken cancellationToken)
    {
        string? basicAuthUser = null;
        if (project.AccessMode == AccessMode.BasicAuth &&
            !string.IsNullOrWhiteSpace(project.BasicAuthUserSecretUri))
        {
            // Resolve the username (non-sensitive) from KV so the UI can pre-fill it.
            // The password is never resolved or returned.
            basicAuthUser = await _secretResolver.ResolveAsync(project.BasicAuthUserSecretUri, cancellationToken);
        }

        return new ProjectAccessDto(
            ProjectId: project.Id,
            AccessMode: project.AccessMode,
            BasicAuthUser: basicAuthUser,
            HeaderTokenName: project.AccessMode == AccessMode.HeaderToken
                ? project.HeaderTokenName
                : null);
    }

    private async Task ClearSecretsAsync(
        string? userUri, string? passUri, string? tokenUri, CancellationToken cancellationToken)
    {
        // Overwrite previous secrets with empty string to invalidate them after the mode switch.
        // Called only after the new Key Vault writes and the Cosmos update have both succeeded.
        if (!string.IsNullOrWhiteSpace(userUri))
            await _secretResolver.StoreAsync(userUri, string.Empty, cancellationToken);

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
}
