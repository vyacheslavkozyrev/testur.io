using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.Api.Services;

public interface IProjectApiAuthService
{
    Task<(ProjectOperationResult Result, ProjectApiAuthDto? Dto)> GetAsync(
        string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, ProjectApiAuthDto? Dto)> UpdateAsync(
        string userId, string projectId, UpdateProjectApiAuthRequest request,
        CancellationToken cancellationToken = default);
}

public partial class ProjectApiAuthService : IProjectApiAuthService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<ProjectApiAuthService> _logger;

    public ProjectApiAuthService(
        IProjectRepository projectRepository,
        ISecretResolver secretResolver,
        ILogger<ProjectApiAuthService> logger)
    {
        _projectRepository = projectRepository;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    public async Task<(ProjectOperationResult Result, ProjectApiAuthDto? Dto)> GetAsync(
        string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        if (project.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        return (ProjectOperationResult.Success, BuildDto(project));
    }

    public async Task<(ProjectOperationResult Result, ProjectApiAuthDto? Dto)> UpdateAsync(
        string userId, string projectId, UpdateProjectApiAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null);

        if (project.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        // Capture old secret URIs before mutations — new KV writes happen first;
        // if they throw, Cosmos is never touched and old config remains intact.
        var oldBearerUri   = project.ApiAuthBearerTokenSecretUri;
        var oldApiKeyUri   = project.ApiAuthApiKeyValueSecretUri;
        var oldBasicPassUri = project.ApiAuthBasicPasswordSecretUri;

        var newMethod = ParseMethod(request.ApiAuthMethod);

        project.ApiAuthMethod = newMethod;
        project.ApiAuthBearerTokenSecretUri = null;
        project.ApiAuthApiKeyName = null;
        project.ApiAuthApiKeyPlacement = ApiAuthApiKeyPlacement.Header;
        project.ApiAuthApiKeyValueSecretUri = null;
        project.ApiAuthBasicUsername = null;
        project.ApiAuthBasicPasswordSecretUri = null;

        switch (newMethod)
        {
            case ApiAuthMethod.Bearer:
                var bearerSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.ApiAuthBearerToken);
                await _secretResolver.StoreAsync(bearerSecretName, request.ApiAuthBearerToken!, cancellationToken);
                project.ApiAuthBearerTokenSecretUri = bearerSecretName;
                if (oldBearerUri == bearerSecretName) oldBearerUri = null;
                LogBearerConfigured(_logger, projectId, userId);
                break;

            case ApiAuthMethod.ApiKey:
                var apiKeySecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.ApiAuthApiKeyValue);
                await _secretResolver.StoreAsync(apiKeySecretName, request.ApiAuthApiKeyValue!, cancellationToken);
                project.ApiAuthApiKeyValueSecretUri = apiKeySecretName;
                project.ApiAuthApiKeyName = request.ApiAuthApiKeyName;
                project.ApiAuthApiKeyPlacement = request.ApiAuthApiKeyPlacement == "query"
                    ? ApiAuthApiKeyPlacement.Query
                    : ApiAuthApiKeyPlacement.Header;
                if (oldApiKeyUri == apiKeySecretName) oldApiKeyUri = null;
                LogApiKeyConfigured(_logger, projectId, userId);
                break;

            case ApiAuthMethod.Basic:
                var basicPassSecretName = ProjectSecretNamespace.SecretName(projectId, ProjectSecretNamespace.ApiAuthBasicPassword);
                await _secretResolver.StoreAsync(basicPassSecretName, request.ApiAuthBasicPassword!, cancellationToken);
                project.ApiAuthBasicPasswordSecretUri = basicPassSecretName;
                project.ApiAuthBasicUsername = request.ApiAuthBasicUsername;
                if (oldBasicPassUri == basicPassSecretName) oldBasicPassUri = null;
                LogBasicConfigured(_logger, projectId, userId);
                break;

            default:
                LogNoneConfigured(_logger, projectId, userId);
                break;
        }

        project.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);

        // Best-effort stale secret cleanup — obsolete secrets with known names are harmless once URIs are removed from Cosmos.
        try
        {
            await ClearSecretsAsync(
                newMethod != ApiAuthMethod.Bearer   ? oldBearerUri    : null,
                newMethod != ApiAuthMethod.ApiKey   ? oldApiKeyUri    : null,
                newMethod != ApiAuthMethod.Basic    ? oldBasicPassUri : null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            LogSecretCleanupFailed(_logger, projectId, ex.Message);
        }

        return (ProjectOperationResult.Success, BuildDto(updated));
    }

    private static ApiAuthMethod ParseMethod(string method) => method switch
    {
        "bearer"  => ApiAuthMethod.Bearer,
        "api_key" => ApiAuthMethod.ApiKey,
        "basic"   => ApiAuthMethod.Basic,
        _         => ApiAuthMethod.None,
    };

    private static ProjectApiAuthDto BuildDto(Project project)
    {
        var method = project.ApiAuthMethod;
        return new ProjectApiAuthDto(
            ProjectId:                     project.Id,
            ApiAuthMethod:                 MethodToString(method),
            ApiAuthBearerTokenConfigured:  method == ApiAuthMethod.Bearer  ? !string.IsNullOrWhiteSpace(project.ApiAuthBearerTokenSecretUri)  : null,
            ApiAuthApiKeyName:             method == ApiAuthMethod.ApiKey  ? project.ApiAuthApiKeyName        : null,
            ApiAuthApiKeyPlacement:        method == ApiAuthMethod.ApiKey  ? PlacementToString(project.ApiAuthApiKeyPlacement) : null,
            ApiAuthApiKeyValueConfigured:  method == ApiAuthMethod.ApiKey  ? !string.IsNullOrWhiteSpace(project.ApiAuthApiKeyValueSecretUri)   : null,
            ApiAuthBasicUsername:          method == ApiAuthMethod.Basic   ? project.ApiAuthBasicUsername     : null,
            ApiAuthBasicPasswordConfigured: method == ApiAuthMethod.Basic  ? !string.IsNullOrWhiteSpace(project.ApiAuthBasicPasswordSecretUri) : null);
    }

    private static string MethodToString(ApiAuthMethod method) => method switch
    {
        ApiAuthMethod.Bearer  => "bearer",
        ApiAuthMethod.ApiKey  => "api_key",
        ApiAuthMethod.Basic   => "basic",
        _                     => "none",
    };

    private static string PlacementToString(ApiAuthApiKeyPlacement placement) => placement switch
    {
        ApiAuthApiKeyPlacement.Query => "query",
        _                           => "header",
    };

    private async Task ClearSecretsAsync(
        string? bearerUri, string? apiKeyUri, string? basicPassUri, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(bearerUri))
            await _secretResolver.StoreAsync(bearerUri, string.Empty, ct);
        if (!string.IsNullOrWhiteSpace(apiKeyUri))
            await _secretResolver.StoreAsync(apiKeyUri, string.Empty, ct);
        if (!string.IsNullOrWhiteSpace(basicPassUri))
            await _secretResolver.StoreAsync(basicPassUri, string.Empty, ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} API auth set to Bearer by user {UserId}")]
    private static partial void LogBearerConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} API auth set to ApiKey by user {UserId}")]
    private static partial void LogApiKeyConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} API auth set to Basic by user {UserId}")]
    private static partial void LogBasicConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} API auth set to None by user {UserId}")]
    private static partial void LogNoneConfigured(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Project {ProjectId} — best-effort API auth secret cleanup failed: {ErrorMessage}")]
    private static partial void LogSecretCleanupFailed(ILogger logger, string projectId, string errorMessage);
}
