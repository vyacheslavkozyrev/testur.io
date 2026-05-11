using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<ProjectDto?> GetAsync(string userId, string projectId, CancellationToken cancellationToken = default);
    Task<ProjectDto> CreateAsync(string userId, CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<ProjectDto?> UpdateAsync(string userId, string projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the project by setting isDeleted = true.
    /// Returns <c>false</c> when the project does not exist or belongs to another user.
    /// </summary>
    Task<bool> DeleteAsync(string userId, string projectId, CancellationToken cancellationToken = default);
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
        };

        // Establish the Key Vault namespace for this project (no Azure SDK call — naming only).
        // The namespace prefix is logged so operators can verify secret isolation in audit logs.
        var namespacePrefix = ProjectSecretNamespace.NamespacePrefix(project.Id);
        LogProjectNamespaceProvisioned(_logger, project.Id, namespacePrefix);

        var created = await _projectRepository.CreateAsync(project, cancellationToken);
        LogProjectCreated(_logger, created.Id, userId);
        return ToDto(created);
    }

    public async Task<ProjectDto?> UpdateAsync(string userId, string projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (existing is null)
            return null;

        existing.Name = request.Name;
        existing.ProductUrl = request.ProductUrl;
        existing.TestingStrategy = request.TestingStrategy;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _projectRepository.UpdateAsync(existing, cancellationToken);
        LogProjectUpdated(_logger, updated.Id, userId);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        var existing = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (existing is null)
            return false;

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _projectRepository.UpdateAsync(existing, cancellationToken);
        LogProjectDeleted(_logger, projectId, userId);
        return true;
    }

    private static ProjectDto ToDto(Project project) => new(
        ProjectId: project.Id,
        Name: project.Name,
        ProductUrl: project.ProductUrl,
        TestingStrategy: project.TestingStrategy,
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
}
