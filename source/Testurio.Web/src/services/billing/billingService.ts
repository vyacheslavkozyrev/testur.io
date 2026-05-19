import apiClient from '@/services/apiClient';
import type {
  CheckoutSessionResponse,
  CreateCheckoutSessionRequest,
  SubscriptionStatusResponse,
} from '@/types/plan.types';

export const billingService = {
  createCheckoutSession: (body: CreateCheckoutSessionRequest): Promise<CheckoutSessionResponse> =>
    apiClient.post<CheckoutSessionResponse>('/v1/billing/checkout', body).then((r) => r.data),

  getSubscriptionStatus: (): Promise<SubscriptionStatusResponse> =>
    apiClient.get<SubscriptionStatusResponse>('/v1/billing/subscription').then((r) => r.data),
};
