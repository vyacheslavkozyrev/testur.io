import { http, HttpResponse } from 'msw';
import type {
  CheckoutSessionResponse,
  PortalSessionResponse,
  SubscriptionStatusResponse,
} from '@/types/plan.types';

const mockCheckoutSessionResponse: CheckoutSessionResponse = {
  checkoutUrl: 'https://checkout.stripe.com/c/pay/mock_session',
};

const mockPortalSessionResponse: PortalSessionResponse = {
  portalUrl: 'https://billing.stripe.com/mock',
};

const mockSubscriptionStatus: SubscriptionStatusResponse = {
  status: 'Active',
  plan: 'TestPro',
  billingInterval: 'monthly',
  trialEndsAt: null,
  currentPeriodEnd: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
  cancelledAt: null,
  paymentMethodLast4: '4242',
  paymentMethodExpMonth: 12,
  paymentMethodExpYear: 2027,
  invoices: [
    {
      date: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
      amount: 29,
      currency: 'usd',
      status: 'paid',
      pdfUrl: 'https://pay.stripe.com/invoice/mock/pdf',
    },
  ],
};

export const billingHandlers = [
  http.post('/v1/billing/checkout', () =>
    HttpResponse.json(mockCheckoutSessionResponse, { status: 200 }),
  ),

  http.get('/v1/billing/subscription', () =>
    HttpResponse.json(mockSubscriptionStatus, { status: 200 }),
  ),

  http.post('/v1/billing/portal-session', () =>
    HttpResponse.json(mockPortalSessionResponse, { status: 200 }),
  ),

  http.post('/v1/billing/reactivate', () => new HttpResponse(null, { status: 204 })),
];
