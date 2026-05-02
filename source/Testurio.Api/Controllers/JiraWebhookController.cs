using Microsoft.AspNetCore.Mvc;
using Testurio.Api.Middleware;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Api.Controllers;

public static class JiraWebhookController
{
    public static IEndpointRouteBuilder MapJiraWebhooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks/jira");

        group.MapPost("/{projectId}", HandleWebhookAsync)
            .AddEndpointFilter(JiraWebhookSignatureFilter.InvokeAsync)
            .WithName("JiraWebhook");

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        string projectId,
        [FromBody] JiraWebhookPayload payload,
        [FromServices] JiraWebhookService webhookService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var project = context.Items["Project"] as Project;
        var userId = project?.UserId ?? string.Empty;

        var result = await webhookService.ProcessAsync(userId, projectId, payload, cancellationToken);

        return result switch
        {
            WebhookProcessResult.Enqueued => TypedResults.Accepted((string?)null),
            _ => TypedResults.Ok()
        };
    }
}
