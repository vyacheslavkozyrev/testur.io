using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string userId, string projectId, CancellationToken cancellationToken = default);
}
