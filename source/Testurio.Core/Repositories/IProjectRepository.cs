using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string userId, string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default);

    /// <remarks>Cross-partition fan-out — only valid for the webhook auth path. Use GetByIdAsync for all other callers.</remarks>
    Task<Project?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default);
}
