using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs.Billing;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder v1)
    {
        var billing = v1.MapGroup("/billing");

        billing.MapPost("/checkout", CreateCheckoutSessionAsync).WithName("CreateCheckoutSession");
        billing.MapGet("/subscription", GetSubscriptionStatusAsync).WithName("GetSubscriptionStatus");
        billing.MapPost("/portal-session", CreatePortalSessionAsync).WithName("CreatePortalSession");
        billing.MapPost("/reactivate", ReactivateSubscriptionAsync).WithName("ReactivateSubscription");

        return v1;
    }

    public static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/stripe", HandleStripeWebhookAsync)
            .AllowAnonymous()
            .WithName("StripeWebhook");

        return app;
    }

    private static async Task<Ok<CheckoutSessionResponse>> CreateCheckoutSessionAsync(
        [Microsoft.AspNetCore.Mvc.FromBody] CreateCheckoutSessionRequest request,
        ClaimsPrincipal user,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var userEmail = user.FindFirstValue("emails")
            ?? user.FindFirstValue("email")
            ?? string.Empty;

        var response = await billingService.CreateCheckoutSessionAsync(userId, userEmail, request, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<SubscriptionStatusResponse>> GetSubscriptionStatusAsync(
        ClaimsPrincipal user,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var response = await billingService.GetSubscriptionStatusAsync(userId, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<PortalSessionResponse>> CreatePortalSessionAsync(
        ClaimsPrincipal user,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var response = await billingService.CreatePortalSessionAsync(userId, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<NoContent> ReactivateSubscriptionAsync(
        ClaimsPrincipal user,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        await billingService.ReactivateSubscriptionAsync(userId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> HandleStripeWebhookAsync(
        HttpContext context,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue("Stripe-Signature", out var signatureHeader) ||
            string.IsNullOrWhiteSpace(signatureHeader))
            return TypedResults.BadRequest();

        if (!context.Request.Body.CanSeek)
            return Results.Problem("An internal configuration error occurred.", statusCode: 500);

        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            await billingService.HandleStripeWebhookAsync(payload, signatureHeader.ToString(), cancellationToken);
            return TypedResults.Ok();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.BadRequest();
        }
    }
}
