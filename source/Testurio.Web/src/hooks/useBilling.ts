import { useQuery, useMutation } from '@tanstack/react-query';
import { billingService } from '@/services/billing/billingService';
import type {
  CheckoutSessionResponse,
  CreateCheckoutSessionRequest,
  SubscriptionStatusResponse,
  SubscriptionStatus,
} from '@/types/plan.types';
import type { ApiError } from '@/types/api.types';

export const BILLING_KEYS = {
  subscription: ['billing', 'subscription'] as const,
};

/**
 * Terminal statuses — polling stops once one of these is reached.
 * 'Expired' is included to prevent an infinite poll loop in the unlikely edge case
 * where the subscription is immediately expired after checkout (e.g. fraud check).
 * AC-023 specifies stopping on Trialing/Active; Expired is a safe additional guard.
 */
const TERMINAL_STATUSES: SubscriptionStatus[] = ['Trialing', 'Active', 'Expired'];

/**
 * Fetches the current user's subscription status.
 * Polls every 3 seconds until a terminal status is reached or `enabled` is false.
 *
 * @param pollUntilTerminal When true (default false), poll every 3 s and stop automatically on terminal status.
 */
export function useSubscriptionStatus(pollUntilTerminal = false) {
  return useQuery<SubscriptionStatusResponse, ApiError>({
    queryKey: BILLING_KEYS.subscription,
    queryFn: billingService.getSubscriptionStatus,
    refetchInterval: (query) => {
      if (!pollUntilTerminal) return false;
      const status = query.state.data?.status;
      if (status && TERMINAL_STATUSES.includes(status)) return false;
      return 3000;
    },
  });
}

/**
 * Initiates a Stripe Checkout session.
 * On success, redirects the browser to the returned checkoutUrl.
 */
export function useCreateCheckoutSession() {
  return useMutation<CheckoutSessionResponse, ApiError, CreateCheckoutSessionRequest>({
    mutationFn: billingService.createCheckoutSession,
    onSuccess: (data) => {
      window.location.href = data.checkoutUrl;
    },
  });
}
