export interface PlanDefinition {
  id: string;
  name: string;
  monthlyPrice: number;
  annualPrice: number;
  annualDiscountPercent: number;
  isPopular: boolean;
  features: string[];
}

export type BillingInterval = 'monthly' | 'annual';

// ─── Subscription types (feature 0015) ───────────────────────────────────────

export type SubscriptionStatus =
  | 'None'
  | 'Trialing'
  | 'Active'
  | 'Expired'
  | 'CancelledPendingExpiry'
  | 'PaymentFailed';

export interface CreateCheckoutSessionRequest {
  plan: string;
  billingInterval: BillingInterval;
}

export interface CheckoutSessionResponse {
  checkoutUrl: string;
}

export interface InvoiceDto {
  date: string;
  amount: number;
  currency: string;
  status: string;
  pdfUrl: string | null;
}

export interface SubscriptionStatusResponse {
  status: SubscriptionStatus;
  plan: string | null;
  billingInterval: BillingInterval | null;
  trialEndsAt: string | null;
  currentPeriodEnd: string;
  cancelledAt: string | null;
  paymentMethodLast4: string | null;
  paymentMethodExpMonth: number | null;
  paymentMethodExpYear: number | null;
  invoices: InvoiceDto[];
}

export interface PortalSessionResponse {
  portalUrl: string;
}
