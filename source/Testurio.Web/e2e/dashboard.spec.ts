import { test, expect } from '@playwright/test';
import type { DashboardResponse } from '../src/types/dashboard.types';

const QUOTA_USAGE = {
  usedToday: 3,
  dailyLimit: 50,
  resetsAt: new Date(Date.now() + 86400000).toISOString(),
};

const MOCK_DASHBOARD_WITH_PROJECTS: DashboardResponse = {
  projects: [
    {
      projectId: 'aaaaaaaa-0000-0000-0000-000000000001',
      name: 'Newer Project',
      productUrl: 'https://newer.example.com',
      testingStrategy: 'API testing',
      latestRun: {
        runId: 'run-001',
        status: 'Passed',
        startedAt: new Date(Date.now() - 3600000).toISOString(),
        completedAt: new Date(Date.now() - 3000000).toISOString(),
      },
    },
    {
      projectId: 'bbbbbbbb-0000-0000-0000-000000000002',
      name: 'Older Project',
      productUrl: 'https://older.example.com',
      testingStrategy: 'UI E2E testing',
      latestRun: {
        runId: 'run-002',
        status: 'Failed',
        startedAt: new Date(Date.now() - 7200000).toISOString(),
        completedAt: new Date(Date.now() - 6600000).toISOString(),
      },
    },
    {
      projectId: 'cccccccc-0000-0000-0000-000000000003',
      name: 'No Run Project',
      productUrl: 'https://norun.example.com',
      testingStrategy: 'API testing',
      latestRun: null,
    },
  ],
  quotaUsage: QUOTA_USAGE,
};

const MOCK_DASHBOARD_EMPTY: DashboardResponse = {
  projects: [],
  quotaUsage: QUOTA_USAGE,
};

const MOCK_USER = {
  id: '00000000-0000-0000-0000-000000000099',
  displayName: 'Jane Smith',
  email: 'jane.smith@example.com',
};

test.describe('Dashboard Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
  });

  test('AC-001: dashboard is accessible at /dashboard', async ({ page }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_WITH_PROJECTS }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveURL('/dashboard');
  });

  test('AC-003/AC-004: card grid renders projects with correct sort order and status badges', async ({
    page,
  }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_WITH_PROJECTS }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    const cards = page.locator('.MuiCard-root');
    // First card: most recent run (Newer Project)
    await expect(cards.first()).toContainText('Newer Project');
    await expect(cards.first()).toContainText('Passed');

    // Second card: older run (Older Project)
    await expect(cards.nth(1)).toContainText('Older Project');
    await expect(cards.nth(1)).toContainText('Failed');

    // Third card: no run project (appears last)
    await expect(cards.nth(2)).toContainText('No Run Project');
    await expect(cards.nth(2)).toContainText('Never run');
  });

  test('AC-007/AC-012: Create Project button is always visible in dashboard header', async ({
    page,
  }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_WITH_PROJECTS }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    await expect(
      page.getByRole('button', { name: /create project/i }),
    ).toBeVisible();
  });

  test('AC-007/AC-012: Create Project button is visible even when no projects exist', async ({
    page,
  }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_EMPTY }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    await expect(
      page.getByRole('button', { name: /create project/i }),
    ).toBeVisible();
  });

  test('AC-009/AC-010/AC-011: empty state CTA visible when no projects, with Create button', async ({
    page,
  }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_EMPTY }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    // Empty state panel must be visible
    await expect(
      page.getByRole('button', { name: /create your first project/i }),
    ).toBeVisible();
  });

  test('AC-013/AC-014: quota bar is visible at top of page with numeric usage', async ({
    page,
  }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_WITH_PROJECTS }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    // Quota bar should contain "3 / 50" or similar
    await expect(page.getByText(/3\s*\/\s*50/)).toBeVisible();
  });

  test('AC-020/AC-021/AC-024: each project card has a link pointing to /projects/:id/history', async ({
    page,
  }) => {
    await page.route('**/v1/stats/dashboard', (route) =>
      route.fulfill({ json: MOCK_DASHBOARD_WITH_PROJECTS }),
    );

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    const projectId = MOCK_DASHBOARD_WITH_PROJECTS.projects[0].projectId;
    // Feature 0010 owns the navigation contract — verify the link href is correct.
    // Actual navigation to the history page is owned by feature 0011.
    const cardLink = page
      .locator(`a[href="/projects/${projectId}/history"]`)
      .first();
    await expect(cardLink).toBeVisible();
    // Verify the link uses client-side routing (href present, not a full-page reload trigger)
    const href = await cardLink.getAttribute('href');
    expect(href).toBe(`/projects/${projectId}/history`);
  });
});
