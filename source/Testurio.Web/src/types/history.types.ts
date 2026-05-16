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

export interface ScenarioSummary {
  title: string;
  passed: boolean;
  durationMs: number;
  errorSummary: string | null;
  testType: 'api' | 'ui_e2e';
  screenshotUris: string[];
}

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
}
