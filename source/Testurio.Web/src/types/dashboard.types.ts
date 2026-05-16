/**
 * Dashboard snapshot types — mirrors the C# domain models in Testurio.Core.
 * DashboardUpdatedEvent is defined here so that feature 0043's useDashboardStream
 * hook can import it without introducing a reverse dependency on this feature.
 */

export type RunStatus =
  | 'Queued'
  | 'Running'
  | 'Passed'
  | 'Failed'
  | 'Cancelled'
  | 'TimedOut'
  | 'NeverRun';

export interface LatestRunSummary {
  runId: string;
  status: RunStatus;
  startedAt: string; // ISO 8601
  completedAt: string | null; // ISO 8601, null when run is still active
}

export interface DashboardProjectSummary {
  projectId: string;
  name: string;
  productUrl: string;
  testingStrategy: string;
  latestRun: LatestRunSummary | null;
}

export interface QuotaUsage {
  usedToday: number;
  dailyLimit: number;
  resetsAt: string; // ISO 8601 — next midnight UTC
}

export interface DashboardResponse {
  projects: DashboardProjectSummary[];
  quotaUsage: QuotaUsage;
}

/**
 * Emitted via SSE when a project's latest run status changes.
 * Defined here (not in feature 0043) so feature 0043's useDashboardStream hook
 * can import without a reverse dependency.
 */
export interface DashboardUpdatedEvent {
  projectId: string;
  latestRun: LatestRunSummary;
  quotaUsage: QuotaUsage | null;
}
