using Testurio.Core.Models;

namespace Testurio.Api.Services;

public interface IJiraWebhookService
{
    Task<WebhookProcessResult> ProcessAsync(string userId, string projectId, JiraWebhookPayload payload, CancellationToken cancellationToken = default);
}
