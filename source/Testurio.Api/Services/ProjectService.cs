using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Constants;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.Api.Services;

/// <summary>
/// Discriminated result for service operations that must distinguish
/// "not found" from "found but belongs to a different user".
/// </summary>
public enum ProjectOperationResult
{
    Success,
    NotFound,
    Forbidden,
}

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<ProjectDto?> GetAsync(string userId, string projectId, CancellationToken cancellationToken = default);
    Task<ProjectDto> CreateAsync(string userId, CreateProjectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a project's core fields.
    /// Returns <c>null</c> on Success, and the result discriminator for NotFound / Forbidden.
    /// </summary>
    Task<(ProjectOperationResult Result, ProjectDto? Dto)> UpdateAsync(string userId, string projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the project by setting isDeleted = true.
    /// Returns the result discriminator so callers can return the correct HTTP status.
    /// </summary>
    Task<ProjectOperationResult> DeleteAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the project DTO when the project is found and belongs to the user.
    /// Returns the result discriminator so callers can distinguish NotFound from Forbidden.
    /// </summary>
    Task<(ProjectOperationResult Result, ProjectDto? Dto)> GetWithOwnershipCheckAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the <c>AllowedWorkItemTypes</c> field on the project document (AC-006).
    /// Returns the result discriminator so callers can return the correct HTTP status.
    /// </summary>
    Task<(ProjectOperationResult Result, ProjectDto? Dto)> UpdateWorkItemTypeFilterAsync(string userId, string projectId, string[] allowedWorkItemTypes, CancellationToken cancellationToken = default);
}

public partial class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IProjectRepository projectRepository, ILogger<ProjectService> logger)
    {
        _projectRepository = projectRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var projects = await _projectRepository.ListByUserAsync(userId, cancellationToken);
        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        return project is null ? null : ToDto(project);
    }

    public async Task<ProjectDto> CreateAsync(string userId, CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = new Project
        {
            UserId = userId,
            Name = request.Name,
            ProductUrl = request.ProductUrl,
            TestingStrategy = request.TestingStrategy,
            CustomPrompt = string.IsNullOrWhiteSpace(request.CustomPrompt) ? null : request.CustomPrompt,
            RequestTimeoutSeconds = request.RequestTimeoutSeconds,
        };

        // Establish the Key Vault namespace for this project (no Azure SDK call — naming only).
        // The namespace prefix is logged so operators can verify secret isolation in audit logs.
        var namespacePrefix = ProjectSecretNamespace.NamespacePrefix(project.Id);
        LogProjectNamespaceProvisioned(_logger, project.Id, namespacePrefix);

        var created = await _projectRepository.CreateAsync(project, cancellationToken);
        LogProjectCreated(_logger, created.Id, userId);
        return ToDto(created);
    }

    public async Task<(ProjectOperationResult Result, ProjectDto? Dto)> UpdateAsync(string userId, string projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        // First check if the project exists at all (cross-partition lookup) so we can
        // distinguish "not found" from "belongs to a different user" (AC-018).
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (anyProject is null)
            return (ProjectOperationResult.NotFound, null);

        if (anyProject.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        // Project belongs to the requesting user — load it via the partition-scoped path for correctness.
        var existing = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (existing is null)
            return (ProjectOperationResult.NotFound, null); // soft-deleted between the two reads

        existing.Name = request.Name;
        existing.ProductUrl = request.ProductUrl;
        existing.TestingStrategy = request.TestingStrategy;
        existing.CustomPrompt = string.IsNullOrWhiteSpace(request.CustomPrompt) ? null : request.CustomPrompt;
        existing.RequestTimeoutSeconds = request.RequestTimeoutSeconds;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _projectRepository.UpdateAsync(existing, cancellationToken);
        LogProjectUpdated(_logger, updated.Id, userId);
        return (ProjectOperationResult.Success, ToDto(updated));
    }

    public async Task<ProjectOperationResult> DeleteAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        // Cross-partition lookup to distinguish "not found" from "forbidden" (AC-031).
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (anyProject is null)
            return ProjectOperationResult.NotFound;

        if (anyProject.UserId != userId)
            return ProjectOperationResult.Forbidden;

        // Confirmed owner — load via partition-scoped path for the actual update.
        var existing = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (existing is null)
            return ProjectOperationResult.NotFound; // soft-deleted between the two reads

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _projectRepository.UpdateAsync(existing, cancellationToken);
        LogProjectDeleted(_logger, projectId, userId);
        return ProjectOperationResult.Success;
    }

    public async Task<(ProjectOperationResult Result, ProjectDto? Dto)> GetWithOwnershipCheckAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (anyProject is null)
            return (ProjectOperationResult.NotFound, null);

        if (anyProject.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return (ProjectOperationResult.NotFound, null); // soft-deleted between the two reads

        return (ProjectOperationResult.Success, ToDto(project));
    }

    public async Task<(ProjectOperationResult Result, ProjectDto? Dto)> UpdateWorkItemTypeFilterAsync(string userId, string projectId, string[] allowedWorkItemTypes, CancellationToken cancellationToken = default)
    {
        var anyProject = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (anyProject is null)
            return (ProjectOperationResult.NotFound, null);

        if (anyProject.UserId != userId)
            return (ProjectOperationResult.Forbidden, null);

        var existing = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (existing is null)
            return (ProjectOperationResult.NotFound, null);

        existing.AllowedWorkItemTypes = allowedWorkItemTypes;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _projectRepository.UpdateAsync(existing, cancellationToken);
        LogWorkItemTypeFilterUpdated(_logger, updated.Id, userId);
        return (ProjectOperationResult.Success, ToDto(updated));
    }

    private static ProjectDto ToDto(Project project) => new(
        ProjectId: project.Id,
        Name: project.Name,
        ProductUrl: project.ProductUrl,
        TestingStrategy: project.TestingStrategy,
        CustomPrompt: project.CustomPrompt,
        AllowedWorkItemTypes: project.AllowedWorkItemTypes,
        RequestTimeoutSeconds: project.RequestTimeoutSeconds == 0
            ? ProjectConstants.RequestTimeoutDefaultSeconds
            : project.RequestTimeoutSeconds,
        CreatedAt: project.CreatedAt,
        UpdatedAt: project.UpdatedAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Key Vault namespace provisioned for project {ProjectId}: prefix={NamespacePrefix}")]
    private static partial void LogProjectNamespaceProvisioned(ILogger logger, string projectId, string namespacePrefix);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} created for user {UserId}")]
    private static partial void LogProjectCreated(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} updated by user {UserId}")]
    private static partial void LogProjectUpdated(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectId} soft-deleted by user {UserId}")]
    private static partial void LogProjectDeleted(ILogger logger, string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Work item type filter updated on project {ProjectId} by user {UserId}")]
    private static partial void LogWorkItemTypeFilterUpdated(ILogger logger, string projectId, string userId);
}
