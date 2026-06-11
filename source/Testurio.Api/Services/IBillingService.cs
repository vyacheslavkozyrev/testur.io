using Testurio.Api.DTOs.Billing;

namespace Testurio.Api.Services;

public interface IBillingService
{
    Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(
        string userId,
        string? userEmail,
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task HandleStripeWebhookAsync(
        string payload,
        string stripeSignature,
        CancellationToken cancellationToken = default);

    Task<PortalSessionResponse> CreatePortalSessionAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task ReactivateSubscriptionAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionStatusResponse> SyncCheckoutSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);
}
