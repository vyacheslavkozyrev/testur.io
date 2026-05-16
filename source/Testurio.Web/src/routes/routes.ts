/**
 * Application route constants.
 * Import these in place of hardcoded path strings to ensure consistency
 * across components, navigation links, and redirect logic.
 */

export const DASHBOARD_ROUTE = '/dashboard';

export const PROJECTS_ROUTE = '/projects';

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
