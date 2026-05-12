using Testurio.Api.DTOs;

namespace Testurio.Api.Services;

public interface IPMToolConnectionService
{
    Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto, IDictionary<string, string[]>? ValidationErrors)>
        SaveADOConnectionAsync(string userId, string projectId, SaveADOConnectionRequest request, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto, IDictionary<string, string[]>? ValidationErrors)>
        SaveJiraConnectionAsync(string userId, string projectId, SaveJiraConnectionRequest request, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, TestConnectionResponse? Response)>
        TestConnectionAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto)>
        RemoveConnectionAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, WebhookSetupResponse? Response)>
        GetWebhookSetupAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, WebhookSetupResponse? Response)>
        RegenerateWebhookSecretAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto)>
        GetIntegrationStatusAsync(string userId, string projectId, CancellationToken cancellationToken = default);

    Task<(ProjectOperationResult Result, PMToolConnectionResponse? Dto, IDictionary<string, string[]>? ValidationErrors)>
        UpdateTokenAsync(string userId, string projectId, UpdateTokenRequest request, CancellationToken cancellationToken = default);
}
