import { http, HttpResponse } from 'msw';
import type { CheckoutSessionResponse, SubscriptionStatusResponse } from '@/types/plan.types';

const mockCheckoutSessionResponse: CheckoutSessionResponse = {
  checkoutUrl: 'https://checkout.stripe.com/c/pay/mock_session',
};

const mockSubscriptionStatus: SubscriptionStatusResponse = {
  status: 'None',
  plan: null,
  billingInterval: null,
  trialEndsAt: null,
};

export const billingHandlers = [
  http.post('/v1/billing/checkout', () =>
    HttpResponse.json(mockCheckoutSessionResponse, { status: 200 }),
  ),

  http.get('/v1/billing/subscription', () =>
    HttpResponse.json(mockSubscriptionStatus, { status: 200 }),
  ),
];
