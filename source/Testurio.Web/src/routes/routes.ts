/**
 * Application route constants.
 * Import these in place of hardcoded path strings to ensure consistency
 * across components, navigation links, and redirect logic.
 */

// ─── Public routes (feature 0012) ────────────────────────────────────────────

export const LANDING_ROUTE = '/';

export const PRICING_ROUTE = '/pricing';

// ─── Auth routes (feature 0013) ───────────────────────────────────────────────

export const SIGN_IN_ROUTE = '/sign-in';

export const SIGN_UP_ROUTE = '/sign-up';

export const FORGOT_PASSWORD_ROUTE = '/forgot-password';

// ─── Authenticated routes ─────────────────────────────────────────────────────

export const DASHBOARD_ROUTE = '/dashboard';

export const PROJECTS_ROUTE = '/projects';

export const NEW_PROJECT_ROUTE = '/projects/new';

// ─── Billing routes (feature 0015) ───────────────────────────────────────────

export const BILLING_ROUTE = '/billing';

export const BILLING_SUCCESS_ROUTE = '/billing/success';

export const SETTINGS_ROUTE = '/settings';

/**
 * Builds the route for a project's test-run history page.
 * @param id - The project UUID
 */
export const PROJECT_HISTORY_ROUTE = (id: string): string =>
  `/projects/${id}/history`;

/**
 * Builds the route for a project's settings page.
 * @param id - The project UUID
 */
export const PROJECT_SETTINGS_ROUTE = (id: string): string =>
  `/projects/${id}/settings`;
