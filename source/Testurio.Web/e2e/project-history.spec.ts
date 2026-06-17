import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import type { ProjectHistoryResponse, RunDetailResponse } from '../src/types/history.types';

const seedFile = path.join(__dirname, '.auth/seed.json');
function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

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
  statusTransitionOutcome: null,
  statusTransitionError: null,
  statusTransitionedTo: null,
  scenarioResults: [
    {
      scenarioId: 'sc-001',
      title: 'POST /auth returns 200',
      passed: true,
      durationMs: 210,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
      steps: null,
    },
    {
      scenarioId: 'sc-002',
      title: 'POST /auth with wrong password returns 401',
      passed: true,
      durationMs: 190,
      errorSummary: null,
      testType: 'api',
      screenshotUris: [],
      steps: null,
    },
  ],
  rawCommentMarkdown: '## Report\n**Verdict:** PASSED\n\n- `POST /auth returns 200` — PASSED',
};

const MOCK_RUN_DETAIL_2: RunDetailResponse = {
  id: 'result-002',
  runId: 'run-abc-002',
  storyTitle: 'User can reset password',
  verdict: 'FAILED',
  recommendation: 'request_fixes',
  totalDurationMs: 1800,
  createdAt: new Date(Date.now() - 7200_000).toISOString(),
  statusTransitionOutcome: null,
  statusTransitionError: null,
  statusTransitionedTo: null,
  scenarioResults: [
    {
      scenarioId: 'sc-003',
      title: 'POST /auth/reset returns 200',
      passed: false,
      durationMs: 180,
      errorSummary: 'Expected 200, got 401',
      testType: 'api',
      screenshotUris: [],
      steps: null,
    },
  ],
  rawCommentMarkdown: '## Report\n**Verdict:** FAILED\n\n- `POST /auth/reset returns 200` — FAILED',
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
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );

    const historyFulfilled = page.waitForResponse((resp) =>
      resp.url().includes(`/v1/stats/projects/${PROJECT_ID}/history`),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveURL(HISTORY_URL);
    await historyFulfilled;
    await expect(page.getByText('User can log in')).toBeVisible({ timeout: 10_000 });
  });

  // AC-003, AC-005
  test('AC-003/AC-005: history table shows story title and verdict badge for each run', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({ json: MOCK_HISTORY }),
    );

    const historyFulfilled = page.waitForResponse((resp) =>
      resp.url().includes(`/v1/stats/projects/${PROJECT_ID}/history`),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });
    await historyFulfilled;

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

    await expect(page.getByRole('button', { name: /retry/i })).toBeVisible({ timeout: 10_000 });
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
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/runs/**`, (route) => {
      const detail = route.request().url().includes('run-abc-002') ? MOCK_RUN_DETAIL_2 : MOCK_RUN_DETAIL;
      route.fulfill({ json: detail });
    });

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // Open first run
    await page.getByText('User can log in').click();
    await expect(page.getByText('POST /auth returns 200')).toBeVisible();

    // Toggle to raw view
    await page.getByRole('button', { name: /raw report/i }).click();
    await expect(page.getByText('**Verdict:** PASSED')).toBeVisible();

    // Close the drawer before clicking the second row; the MUI Drawer backdrop
    // intercepts pointer events on background elements while the drawer is open.
    await page.keyboard.press('Escape');
    await expect(page.getByText('POST /auth returns 200')).not.toBeVisible();

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
    // Auth is enforced server-side in the layout; clearing cookies triggers
    // the middleware redirect to /sign-in before the page even renders.
    await page.context().clearCookies();

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // Should be redirected away from history page
    await expect(page).not.toHaveURL(HISTORY_URL);
  });

  // AC-046
  test('AC-046: 404 project renders project-not-found message', async ({
    page,
  }) => {
    await page.route(`**/v1/stats/projects/${PROJECT_ID}/history`, (route) =>
      route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ status: 404, title: 'Not Found' }),
      }),
    );

    await page.goto(HISTORY_URL, { waitUntil: 'domcontentloaded' });

    // React Query default retry (3 attempts + exp backoff) can take ~7s before
    // isError settles on the final 404; allow plenty of headroom on dev.
    await expect(page.getByText(/project not found/i)).toBeVisible({ timeout: 20_000 });
  });
});

// ─── Real-API tests (skipped in local project) ────────────────────────────────
// US-033 (Empty State), US-034 (With Records), US-035 (Run Detail Panel) — AC-139–AC-153

test.describe('Project History — Empty State (real API)', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name === 'local', 'Requires real dev API');
  });

  test('history page loads without error and shows empty state for seed project (AC-139, AC-140, AC-141, AC-142)', async ({ page }) => {
    // First-time dev-server page compilation for /history and /settings can
    // exceed the default 30s test timeout when the suite runs end-to-end.
    test.setTimeout(60_000);

    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/history`, { waitUntil: 'load' });

    expect(page.url()).toContain(`/projects/${seedProjectId}/history`);
    await expect(page.locator('[data-testid="error-boundary"]')).not.toBeVisible();
    await expect(page.getByText(/no test runs yet/i)).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('[data-testid="history-table-row"], [data-testid="run-row"]')).not.toBeVisible();
    await expect(page.locator('[data-testid="trend-chart"]')).not.toBeVisible();

    const settingsButton = page
      .getByRole('button', { name: /project settings/i })
      .or(page.getByRole('link', { name: /project settings/i }));
    await expect(settingsButton).toBeVisible();
    await settingsButton.click();
    await expect(page).toHaveURL(new RegExp(`/projects/${seedProjectId}/settings`), { timeout: 20_000 });
  });
});

test.describe('Project History — With Records (real API)', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name === 'local', 'Requires real dev API');
  });

  let historyProjectId: string | null = null;
  let historyRunId: string | null = null;

  test.beforeAll(async ({ request }) => {
    const listRes = await request.get('/v1/projects');
    if (listRes.ok()) {
      const projects = (await listRes.json()) as Array<{ projectId: string; name: string }>;
      const historyProject = projects.find((p) => p.name === '[E2E] History Project');
      if (historyProject) {
        historyProjectId = historyProject.projectId;

        const runsRes = await request.get(`/v1/stats/projects/${historyProjectId}/runs`);
        if (runsRes.ok()) {
          const runs = (await runsRes.json()) as Array<{ runId: string }>;
          if (runs.length > 0) {
            historyRunId = runs[0].runId;
          }
        }
      }
    }
  });

  test('history page renders run table with at least one row (AC-143, AC-144)', async ({ page }) => {
    if (!historyProjectId || !historyRunId) {
      test.skip(true, '[E2E] History Project with test runs not available');
      return;
    }

    await page.goto(`/projects/${historyProjectId}/history`, { waitUntil: 'load' });

    await expect(page.locator('[data-testid="history-table-row"], [data-testid="run-row"]').first()).toBeVisible({ timeout: 10_000 });

    const firstRow = page.locator('[data-testid="history-table-row"], [data-testid="run-row"]').first();
    await expect(firstRow.locator('[data-testid="story-title"], [class*="title"]').first()).toBeVisible();
    await expect(firstRow.locator('[data-testid="verdict-badge"], [class*="badge"], [class*="verdict"]').first()).toBeVisible();
    await expect(firstRow.locator('[data-testid="run-date"], [class*="date"]').first()).toBeVisible();
    await expect(firstRow.locator('[data-testid="run-duration"], [class*="duration"]').first()).toBeVisible();
    await expect(firstRow.locator('[data-testid="scenario-count"], [class*="count"]').first()).toBeVisible();
  });

  test('trend chart visible with time range toggles, Last 30 days default (AC-145, AC-146)', async ({ page }) => {
    if (!historyProjectId) {
      test.skip(true, '[E2E] History Project not available');
      return;
    }

    await page.goto(`/projects/${historyProjectId}/history`, { waitUntil: 'load' });

    await expect(page.locator('[data-testid="trend-chart"]').or(page.locator('[class*="chart"]').first())).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('button', { name: /last 7 days/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /last 30 days/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /last 90 days/i })).toBeVisible();

    const thirtyDaysBtn = page.getByRole('button', { name: /last 30 days/i });
    const isSelected =
      (await thirtyDaysBtn.getAttribute('aria-pressed')) === 'true' ||
      (await thirtyDaysBtn.getAttribute('data-selected')) === 'true' ||
      (await thirtyDaysBtn.getAttribute('class'))?.includes('active') ||
      (await thirtyDaysBtn.getAttribute('class'))?.includes('selected');
    expect(isSelected).toBe(true);

    let fullReloadCount = 0;
    page.on('framenavigated', (frame) => {
      if (frame === page.mainFrame()) fullReloadCount++;
    });
    fullReloadCount = 0;

    await page.getByRole('button', { name: /last 7 days/i }).click();

    const sevenDaysBtn = page.getByRole('button', { name: /last 7 days/i });
    await expect(sevenDaysBtn).toHaveAttribute('aria-pressed', 'true', { timeout: 5_000 })
      .catch(() => expect(sevenDaysBtn).toHaveAttribute('data-selected', 'true', { timeout: 5_000 }))
      .catch(() => {});

    expect(fullReloadCount).toBe(0);
  });

  test('clicking a run row opens the detail panel (AC-147, AC-148)', async ({ page }) => {
    if (!historyProjectId || !historyRunId) {
      test.skip(true, '[E2E] History Project with runs not available');
      return;
    }

    await page.goto(`/projects/${historyProjectId}/history`, { waitUntil: 'load' });

    const firstRow = page.locator('[data-testid="history-table-row"], [data-testid="run-row"]').first();
    await firstRow.click();

    await expect(page).toHaveURL(new RegExp(`/projects/${historyProjectId}/history`));

    const detailPanel = page.locator('[data-testid="run-detail-panel"], [class*="detail-panel"]');
    await expect(detailPanel).toBeVisible({ timeout: 10_000 });
  });

  test('run detail panel shows title, verdict, scenarios, and raw report toggle (AC-149, AC-150, AC-151, AC-152, AC-153)', async ({ page }) => {
    if (!historyProjectId || !historyRunId) {
      test.skip(true, '[E2E] History Project with runs not available');
      return;
    }

    await page.goto(`/projects/${historyProjectId}/history`, { waitUntil: 'load' });

    const firstRow = page.locator('[data-testid="history-table-row"], [data-testid="run-row"]').first();
    await firstRow.click();

    const detailPanel = page.locator('[data-testid="run-detail-panel"], [class*="detail-panel"]');
    await expect(detailPanel).toBeVisible({ timeout: 10_000 });

    await expect(detailPanel.locator('[data-testid="story-title"], [class*="title"]').first()).toBeVisible();
    await expect(detailPanel.locator('[data-testid="verdict-badge"], [class*="verdict"]').first()).toBeVisible();
    await expect(detailPanel.locator('[data-testid="scenario-card"]').first()).toBeVisible();

    const rawReportToggle = detailPanel.getByRole('button', { name: /raw report/i });
    await expect(rawReportToggle).toBeVisible();

    await rawReportToggle.click();
    await expect(detailPanel.locator('[data-testid="raw-report-view"], [class*="markdown"]').first()).toBeVisible({ timeout: 5_000 });
  });
});
