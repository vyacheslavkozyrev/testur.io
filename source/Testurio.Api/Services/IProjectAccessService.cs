using Testurio.Api.DTOs;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Services;

public interface IProjectAccessService
{
    Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> GetAsync(
        string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, ProjectAccessDto? Dto)> UpdateAsync(
        string userId, string projectId, UpdateProjectAccessRequest request,
        CancellationToken cancellationToken = default);
}
