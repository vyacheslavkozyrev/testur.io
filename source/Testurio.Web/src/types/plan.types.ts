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

export type SubscriptionStatus = 'None' | 'Trialing' | 'Active' | 'Expired';

export interface CreateCheckoutSessionRequest {
  plan: string;
  billingInterval: BillingInterval;
}

export interface CheckoutSessionResponse {
  checkoutUrl: string;
}

export interface SubscriptionStatusResponse {
  status: SubscriptionStatus;
  plan: string | null;
  billingInterval: BillingInterval | null;
  trialEndsAt: string | null;
}
