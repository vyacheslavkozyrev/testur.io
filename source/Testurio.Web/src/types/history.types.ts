/**
 * Per-project test history and run detail types.
 * Mirrors the C# domain models in Testurio.Core and API DTOs in Testurio.Api.
 */

export interface TrendPoint {
  /** UTC calendar date in ISO 8601 format (YYYY-MM-DD). */
  date: string;
  passed: number;
  failed: number;
}

export interface RunHistoryItem {
  id: string;
  runId: string;
  storyTitle: string;
  verdict: 'PASSED' | 'FAILED';
  recommendation: string;
  totalApiScenarios: number;
  passedApiScenarios: number;
  totalUiE2eScenarios: number;
  passedUiE2eScenarios: number;
  totalDurationMs: number;
  /** ISO 8601 UTC timestamp. */
  createdAt: string;
}

export interface ProjectHistoryResponse {
  runs: RunHistoryItem[];
  trendPoints: TrendPoint[];
}

export interface StepSummary {
  /** 1-based index of the step within the scenario's step list. */
  stepIndex: number;
  /** Action discriminator: navigate | click | fill | assert_visible | assert_text | assert_url */
  action: string;
  passed: boolean;
  errorMessage: string | null;
  screenshotBlobUri: string | null;
}

export interface ScenarioSummary {
  scenarioId: string;
  title: string;
  passed: boolean;
  durationMs: number;
  errorSummary: string | null;
  testType: 'api' | 'ui_e2e';
  screenshotUris: string[];
  /**
   * Per-step execution detail for ui_e2e scenarios; null for api scenarios.
   * Existing TestResult documents that pre-date this feature will have null here.
   */
  steps: StepSummary[] | null;
}

/** Outcome of the automatic PM tool work item status transition. */
export type StatusTransitionOutcome = 'succeeded' | 'failed' | 'notConfigured';

export interface RunDetailResponse {
  id: string;
  runId: string;
  storyTitle: string;
  verdict: 'PASSED' | 'FAILED';
  recommendation: string;
  totalDurationMs: number;
  /** ISO 8601 UTC timestamp. */
  createdAt: string;
  scenarioResults: ScenarioSummary[];
  rawCommentMarkdown: string | null;
  /** Post-run status transition outcome. Null when the transition step has not yet run. */
  statusTransitionOutcome: StatusTransitionOutcome | null;
  /** Error detail when statusTransitionOutcome is 'failed'. Null otherwise. */
  statusTransitionError: string | null;
  /** The PM tool status name the work item was transitioned to. Null when not configured or failed. */
  statusTransitionedTo: string | null;
}
