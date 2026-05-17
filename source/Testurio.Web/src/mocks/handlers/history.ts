import { http, HttpResponse } from 'msw';
import type { ProjectHistoryResponse, RunDetailResponse } from '@/types/history.types';

const PROJECT_ID = '00000000-0000-0000-0000-000000000001';
const RUN_ID_1 = '00000000-0000-0000-0000-000000000011';
const RUN_ID_2 = '00000000-0000-0000-0000-000000000012';

/** Generate 90 consecutive trend points ending today. */
function buildTrendPoints() {
  const today = new Date();
  return Array.from({ length: 90 }, (_, i) => {
    const d = new Date(today);
    d.setUTCDate(today.getUTCDate() - (89 - i));
    const date = d.toISOString().slice(0, 10);
    // Sprinkle some non-zero values to make the chart interesting.
    const passed = i % 7 === 0 ? 2 : i % 3 === 0 ? 1 : 0;
    const failed = i % 11 === 0 ? 1 : 0;
    return { date, passed, failed };
  });
}

const mockHistoryResponse: ProjectHistoryResponse = {
  runs: [
    {
      id: '00000000-0000-0000-0000-000000000101',
      runId: RUN_ID_1,
      storyTitle: 'User can reset their password via email',
      verdict: 'PASSED',
      recommendation: 'approve',
      totalApiScenarios: 3,
      passedApiScenarios: 3,
      totalUiE2eScenarios: 1,
      passedUiE2eScenarios: 1,
      totalDurationMs: 12340,
      createdAt: '2026-05-16T10:05:00Z',
    },
    {
      id: '00000000-0000-0000-0000-000000000102',
      runId: RUN_ID_2,
      storyTitle: 'Admin can export user list as CSV',
      verdict: 'FAILED',
      recommendation: 'request_fixes',
      totalApiScenarios: 2,
      passedApiScenarios: 1,
      totalUiE2eScenarios: 0,
      passedUiE2eScenarios: 0,
      totalDurationMs: 5210,
      createdAt: '2026-05-15T14:22:00Z',
    },
  ],
  trendPoints: buildTrendPoints(),
};

const mockRunDetailResponse: RunDetailResponse = {
  id: '00000000-0000-0000-0000-000000000101',
  runId: RUN_ID_1,
  storyTitle: 'User can reset their password via email',
  verdict: 'PASSED',
  recommendation: 'approve',
  totalDurationMs: 12340,
  createdAt: '2026-05-16T10:05:00Z',
  scenarioResults: [
    {
      scenarioId: '00000000-0000-0000-0001-000000000001',
      title: 'POST /auth/reset — valid email returns 200',
      passed: true,
      durationMs: 320,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
    },
    {
      scenarioId: '00000000-0000-0000-0001-000000000002',
      title: 'POST /auth/reset — unknown email returns 404',
      passed: true,
      durationMs: 290,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
    },
    {
      scenarioId: '00000000-0000-0000-0001-000000000003',
      title: 'POST /auth/reset — malformed email returns 400',
      passed: true,
      durationMs: 210,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
    },
    {
      scenarioId: '00000000-0000-0000-0001-000000000004',
      title: 'Reset password link navigates to set-new-password screen',
      passed: true,
      durationMs: 11520,
      errorSummary: null,
      testType: 'ui_e2e',
      screenshotUris: [],
    },
  ],
  rawCommentMarkdown:
    '## Test Report\n\n**Verdict:** PASSED\n\n**Recommendation:** approve\n\nAll 4 scenarios passed in 12.34 s.',
};

export const historyHandlers = [
  http.get('/v1/stats/projects/:projectId/history', () =>
    HttpResponse.json(mockHistoryResponse),
  ),

  http.get('/v1/stats/projects/:projectId/runs/:runId', () =>
    HttpResponse.json(mockRunDetailResponse),
  ),
];
