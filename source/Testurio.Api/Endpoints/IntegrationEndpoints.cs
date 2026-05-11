using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Middleware;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1").RequireAuthorization();
        var integrations = v1.MapGroup("/projects/{projectId}/integrations");

        // T012 — GET integration status
        integrations.MapGet("/", GetIntegrationStatusAsync).WithName("GetIntegrationStatus");

        // T010 — Save ADO connection
        integrations.MapPost("/ado", SaveADOConnectionAsync).WithName("SaveADOConnection")
            .AddEndpointFilter<ValidationFilter<SaveADOConnectionRequest>>();

        // T010 — Save Jira connection
        integrations.MapPost("/jira", SaveJiraConnectionAsync).WithName("SaveJiraConnection")
            .AddEndpointFilter<ValidationFilter<SaveJiraConnectionRequest>>();

        // T010 — Remove connection
        integrations.MapDelete("/", RemoveConnectionAsync).WithName("RemoveIntegration");

        // T010 — Test connection
        integrations.MapPost("/test-connection", TestConnectionAsync).WithName("TestConnection");

        // T011 — Webhook setup info
        integrations.MapGet("/webhook-setup", GetWebhookSetupAsync).WithName("GetWebhookSetup");

        // T011 — Regenerate webhook secret
        integrations.MapPost("/webhook-setup/regenerate", RegenerateWebhookSecretAsync).WithName("RegenerateWebhookSecret");

        // US-006 — Update token (inline form after auth_error)
        integrations.MapPatch("/token", UpdateTokenAsync).WithName("UpdateIntegrationToken");

        return app;
    }

    // ─── GET /v1/projects/{projectId}/integrations ───────────────────────────

    private static async Task<Results<Ok<PMToolConnectionResponse>, NotFound, ForbidHttpResult>>
        GetIntegrationStatusAsync(
            string projectId,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await integrationService.GetIntegrationStatusAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }

    // ─── POST /v1/projects/{projectId}/integrations/ado ─────────────────────

    private static async Task<Results<Ok<PMToolConnectionResponse>, NotFound, ForbidHttpResult, ValidationProblem>>
        SaveADOConnectionAsync(
            string projectId,
            SaveADOConnectionRequest request,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto, errors) = await integrationService.SaveADOConnectionAsync(userId, projectId, request, cancellationToken);

        if (errors is { Count: > 0 })
        {
            var problemErrors = errors
                .Select((e, i) => (Key: i.ToString(), Value: e))
                .ToDictionary(t => t.Key, t => new[] { t.Value });
            return TypedResults.ValidationProblem(problemErrors);
        }

        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }

    // ─── POST /v1/projects/{projectId}/integrations/jira ────────────────────

    private static async Task<Results<Ok<PMToolConnectionResponse>, NotFound, ForbidHttpResult, ValidationProblem>>
        SaveJiraConnectionAsync(
            string projectId,
            SaveJiraConnectionRequest request,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto, errors) = await integrationService.SaveJiraConnectionAsync(userId, projectId, request, cancellationToken);

        if (errors is { Count: > 0 })
        {
            var problemErrors = errors
                .Select((e, i) => (Key: i.ToString(), Value: e))
                .ToDictionary(t => t.Key, t => new[] { t.Value });
            return TypedResults.ValidationProblem(problemErrors);
        }

        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }

    // ─── DELETE /v1/projects/{projectId}/integrations ────────────────────────

    private static async Task<Results<Ok<PMToolConnectionResponse>, NotFound, ForbidHttpResult>>
        RemoveConnectionAsync(
            string projectId,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await integrationService.RemoveConnectionAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }

    // ─── POST /v1/projects/{projectId}/integrations/test-connection ──────────

    private static async Task<Results<Ok<TestConnectionResponse>, NotFound, ForbidHttpResult>>
        TestConnectionAsync(
            string projectId,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, response) = await integrationService.TestConnectionAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(response!),
        };
    }

    // ─── GET /v1/projects/{projectId}/integrations/webhook-setup ────────────

    private static async Task<Results<Ok<WebhookSetupResponse>, NotFound, ForbidHttpResult>>
        GetWebhookSetupAsync(
            string projectId,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, response) = await integrationService.GetWebhookSetupAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(response!),
        };
    }

    // ─── POST /v1/projects/{projectId}/integrations/webhook-setup/regenerate ─

    private static async Task<Results<Ok<WebhookSetupResponse>, NotFound, ForbidHttpResult>>
        RegenerateWebhookSecretAsync(
            string projectId,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, response) = await integrationService.RegenerateWebhookSecretAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(response!),
        };
    }

    // ─── PATCH /v1/projects/{projectId}/integrations/token ───────────────────

    private static async Task<Results<Ok<PMToolConnectionResponse>, NotFound, ForbidHttpResult, ValidationProblem>>
        UpdateTokenAsync(
            string projectId,
            UpdateTokenRequest request,
            ClaimsPrincipal user,
            IPMToolConnectionService integrationService,
            CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto, errors) = await integrationService.UpdateTokenAsync(userId, projectId, request, cancellationToken);

        if (errors is { Count: > 0 })
        {
            var problemErrors = errors
                .Select((e, i) => (Key: i.ToString(), Value: e))
                .ToDictionary(t => t.Key, t => new[] { t.Value });
            return TypedResults.ValidationProblem(problemErrors);
        }

        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }
}
