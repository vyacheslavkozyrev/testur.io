import apiClient from '@/services/apiClient';
import type {
  CheckoutSessionResponse,
  CreateCheckoutSessionRequest,
  PortalSessionResponse,
  SubscriptionStatusResponse,
} from '@/types/plan.types';

export const billingService = {
  createCheckoutSession: (body: CreateCheckoutSessionRequest): Promise<CheckoutSessionResponse> =>
    apiClient.post<CheckoutSessionResponse>('/v1/billing/checkout', body).then((r) => r.data),

  getSubscriptionStatus: (): Promise<SubscriptionStatusResponse> =>
    apiClient.get<SubscriptionStatusResponse>('/v1/billing/subscription').then((r) => r.data),

  createPortalSession: (): Promise<PortalSessionResponse> =>
    apiClient.post<PortalSessionResponse>('/v1/billing/portal-session').then((r) => r.data),

  reactivateSubscription: (): Promise<void> =>
    apiClient.post('/v1/billing/reactivate').then(() => undefined),

  syncCheckoutSession: (sessionId: string): Promise<SubscriptionStatusResponse> =>
    apiClient
      .post<SubscriptionStatusResponse>('/v1/billing/sync-session', { sessionId })
      .then((r) => r.data),
};
