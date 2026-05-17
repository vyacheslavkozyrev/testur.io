import { test, expect } from '@playwright/test';
import type { ProjectHistoryResponse, RunDetailResponse } from '../src/types/history.types';

// ─── Shared fixtures ───────────────────────────────────────────────────────────

const PROJECT_ID = 'aaaaaaaa-0000-0000-0000-000000000001';
const RUN_ID = 'run-abc-001';
const HISTORY_URL = `/projects/${PROJECT_ID}/history`;

function makeTrendPoints(): ProjectHistoryResponse['trendPoints'] {
  const points = [];
  const today = new Date();
  for (let i = 89; i >= 0; i--) {
    const d = new Date(today);
    d.setUTCDate(d.getUTCDate() - i);
    const date = d.toISOString().split('T')[0];
    points.push({ date, passed: i % 7 === 0 ? 2 : 0, failed: i % 7 === 3 ? 1 : 0 });
  }
  return points;
}

const MOCK_HISTORY: ProjectHistoryResponse = {
  runs: [
    {
      id: 'result-001',
      runId: RUN_ID,
      storyTitle: 'User can log in',
      verdict: 'PASSED',
      recommendation: 'approve',
      totalApiScenarios: 2,
      passedApiScenarios: 2,
      totalUiE2eScenarios: 0,
      passedUiE2eScenarios: 0,
      totalDurationMs: 4200,
      createdAt: new Date(Date.now() - 3600_000).toISOString(),
    },
    {
      id: 'result-002',
      runId: 'run-abc-002',
      storyTitle: 'User can reset password',
      verdict: 'FAILED',
      recommendation: 'request_fixes',
      totalApiScenarios: 1,
      passedApiScenarios: 0,
      totalUiE2eScenarios: 0,
      passedUiE2eScenarios: 0,
      totalDurationMs: 1800,
      createdAt: new Date(Date.now() - 7200_000).toISOString(),
    },
  ],
  trendPoints: makeTrendPoints(),
};

const MOCK_RUN_DETAIL: RunDetailResponse = {
  id: 'result-001',
  runId: RUN_ID,
  storyTitle: 'User can log in',
  verdict: 'PASSED',
  recommendation: 'approve',
  totalDurationMs: 4200,
  createdAt: new Date(Date.now() - 3600_000).toISOString(),
  scenarioResults: [
    {
      scenarioId: 'sc-001',
      title: 'POST /auth returns 200',
      passed: true,
      durationMs: 210,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
    },
    {
      scenarioId: 'sc-002',
      title: 'POST /auth with wrong password returns 401',
      passed: true,
      durationMs: 190,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
    },
  ],
  rawCommentMarkdown: '## Report\n**Verdict:** PASSED\n\n- `POST /auth returns 200` — PASSED',
};

const MOCK_USER = {
  id: '00000000-0000-0000-0000-000000000099',
  displayName: 'Jane Smith',
  email: 'jane.smith@example.com',
};

// ─── Tests ─────────────────────────────────────────────────────────────────────

test.describe('Project History Page (0011)', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
  });

  // AC-001, AC-002
  test('AC-001/AC-002: history page is accessible and fetches data from history endpoint', async ({
    page,
  }) => {
    let historyRequested = false;
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) => {
      historyRequested = true;
      route.fulfill({ json: MOCK_HISTORY });
    });

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveURL(HISTORY_URL);
    expect(historyRequested).toBe(true);
  });

  // AC-003, AC-005
  test('AC-003/AC-005: history table shows story title and verdict badge for each run', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('User can log in')).toBeVisible();
    await expect(page.getByText('User can reset password')).toBeVisible();
    // Verdict badges should be present
    await expect(page.getByText(/passed/i).first()).toBeVisible();
    await expect(page.getByText(/failed/i).first()).toBeVisible();
  });

  // AC-007
  test('AC-007: loading skeleton is shown while data is fetching', async ({
    page,
  }) => {
    let resolveRoute: () => void;
    const routeBlocked = new Promise<void>((resolve) => { resolveRoute = resolve; });

    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, async (route) => {
      await routeBlocked;
      route.fulfill({ json: MOCK_HISTORY });
    });

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });
    // Skeleton should be visible before data arrives
    await expect(page.locator('[class*="MuiSkeleton"]').first()).toBeVisible();

    resolveRoute!();
    await expect(page.getByText('User can log in')).toBeVisible();
  });

  // AC-008
  test('AC-008: error state with Retry button shown on API failure', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ status: 500, body: 'Internal Server Error' }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.getByRole('button', { name: /retry/i })).toBeVisible();
  });

  // AC-009
  test('AC-009: empty state message shown when project has no runs', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: { runs: [], trendPoints: [] } }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.getByText(/no test runs yet/i)).toBeVisible();
  });

  // AC-010, AC-040
  test('AC-010/AC-040: Project Settings button navigates to /projects/:id/settings', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    const btn = page.getByRole('link', { name: /project settings/i });
    await expect(btn).toHaveAttribute('href', `/projects/${PROJECT_ID}/settings`);
  });

  // AC-011, AC-012, AC-014
  test('AC-011/AC-012/AC-014: trend chart renders with time-range toggles, default 30 days', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // Three toggle buttons must be present
    await expect(page.getByRole('button', { name: /last 7 days/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /last 30 days/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /last 90 days/i })).toBeVisible();

    // Clicking a toggle does not trigger a new network request
    let extraRequests = 0;
    page.on('request', (req) => {
      if (req.url().includes('/history')) extraRequests++;
    });
    await page.getByRole('button', { name: /last 7 days/i }).click();
    await page.getByRole('button', { name: /last 90 days/i }).click();
    expect(extraRequests).toBe(0);
  });

  // AC-006, AC-019, AC-022
  test('AC-006/AC-019/AC-022: clicking a row opens run detail panel', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/runs/${RUN_ID}`, (route) =>
      route.fulfill({ json: MOCK_RUN_DETAIL }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // Click first run row
    await page.getByText('User can log in').click();

    // Panel should open and show scenario titles
    await expect(page.getByText('POST /auth returns 200')).toBeVisible();
    await expect(page.getByText('POST /auth with wrong password returns 401')).toBeVisible();
  });

  // AC-026, AC-028, AC-031
  test('AC-026/AC-028/AC-031: Raw report toggle switches to markdown view and resets on row change', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/runs/**`, (route) =>
      route.fulfill({ json: MOCK_RUN_DETAIL }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // Open first run
    await page.getByText('User can log in').click();
    await expect(page.getByText('POST /auth returns 200')).toBeVisible();

    // Toggle to raw view
    await page.getByRole('button', { name: /raw report/i }).click();
    await expect(page.getByText('**Verdict:** PASSED')).toBeVisible();

    // Switch to second run — raw toggle should reset to structured view
    await page.getByText('User can reset password').click();
    await expect(page.getByText(/request fixes/i)).toBeVisible();
    // Raw markdown for the first run should no longer be visible
    await expect(page.getByText('**Verdict:** PASSED')).not.toBeVisible();
  });

  // AC-042, AC-043
  test('AC-042/AC-043: unauthenticated access redirects to login', async ({
    page,
  }) => {
    // Override auth mock to return 401
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ status: 401, body: 'Unauthorized' }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // Should be redirected away from history page
    await expect(page).not.toHaveURL(HISTORY_URL);
  });

  // AC-046
  test('AC-046: 404 project renders project-not-found message', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ status: 404, body: 'Not Found' }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.getByText(/project not found/i)).toBeVisible();
  });
});
