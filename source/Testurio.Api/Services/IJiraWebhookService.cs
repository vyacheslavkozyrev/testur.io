using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Api.Services;

public interface IJiraWebhookService
{
    Task<WebhookProcessResult> ProcessAsync(Project project, JiraWebhookPayload payload, CancellationToken cancellationToken = default);
}
