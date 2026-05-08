using Testurio.Api.Middleware;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Api.Controllers;

public static class JiraWebhookController
{
    public static IEndpointRouteBuilder MapJiraWebhooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(WebhookRouteConstants.JiraPrefix).AllowAnonymous();

        group.MapPost("/{projectId}", HandleWebhookAsync)
            .AddEndpointFilter<JiraWebhookSignatureFilter>()
            .WithName("JiraWebhook");

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        string projectId,
        JiraWebhookPayload payload,
        IJiraWebhookService webhookService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var project = (Project)context.Items["Project"]!;
        var result = await webhookService.ProcessAsync(project, payload, cancellationToken);

        return result switch
        {
            WebhookProcessResult.Enqueued or WebhookProcessResult.Queued => TypedResults.Accepted((string?)null),
            _ => TypedResults.Ok()
        };
    }
}
