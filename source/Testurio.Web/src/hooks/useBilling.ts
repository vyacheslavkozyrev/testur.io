import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { billingService } from '@/services/billing/billingService';
import type {
  CheckoutSessionResponse,
  CreateCheckoutSessionRequest,
  PortalSessionResponse,
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
 * Refetches on window focus so returning from the Stripe portal reflects the latest state.
 *
 * @param pollUntilTerminal When true, poll every 3 s and stop automatically on terminal status.
 * @param enabled When false, disables the query entirely (no fetch, no polling, no window-focus refetch).
 */
export function useSubscriptionStatus(pollUntilTerminal = false, enabled = true) {
  return useQuery<SubscriptionStatusResponse, ApiError>({
    queryKey: BILLING_KEYS.subscription,
    queryFn: billingService.getSubscriptionStatus,
    enabled,
    refetchOnWindowFocus: true,
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

/**
 * Creates a Stripe Customer Portal session.
 * On success, redirects the browser to the portal URL.
 * On error, the error is surfaced to the caller for display.
 */
export function useCreatePortalSession() {
  return useMutation<PortalSessionResponse, ApiError>({
    mutationFn: billingService.createPortalSession,
    onSuccess: (data) => {
      window.location.href = data.portalUrl;
    },
  });
}

/**
 * Reactivates a cancelled-pending-expiry subscription.
 * On success, invalidates the subscription status cache so the UI reflects Active status.
 */
export function useReactivateSubscription() {
  const qc = useQueryClient();
  return useMutation<void, ApiError>({
    mutationFn: billingService.reactivateSubscription,
    onSuccess: () => qc.invalidateQueries({ queryKey: BILLING_KEYS.subscription }),
  });
}
