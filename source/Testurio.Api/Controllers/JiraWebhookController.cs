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
        var group = app.MapGroup("/v1/webhooks/jira");

        group.MapPost("/{projectId}", HandleWebhookAsync)
            .AddEndpointFilter(JiraWebhookSignatureFilter.InvokeAsync)
            .WithName("JiraWebhook");

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        string projectId,
        [FromBody] JiraWebhookPayload payload,
        [FromServices] IJiraWebhookService webhookService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var project = context.Items["Project"] as Project;
        if (project is null)
            return TypedResults.Unauthorized();

        var result = await webhookService.ProcessAsync(project.UserId, projectId, payload, cancellationToken);

        return result switch
        {
            WebhookProcessResult.Enqueued or WebhookProcessResult.Queued => TypedResults.Accepted((string?)null),
            _ => TypedResults.Ok()
        };
    }
}
