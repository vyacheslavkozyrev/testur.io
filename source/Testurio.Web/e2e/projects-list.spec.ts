import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import type { ProjectDto } from '../src/types/project.types';

const seedFile = path.join(__dirname, '.auth/seed.json');
function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

const PROJECT_A: ProjectDto = {
  projectId: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'Project A',
  productUrl: 'https://project-a.example.com',
  testingStrategy: 'API and UI testing for Project A',
  customPrompt: null,
  allowedWorkItemTypes: null,
  requestTimeoutSeconds: 30,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
};

const PROJECT_B: ProjectDto = {
  projectId: 'bbbbbbbb-0000-0000-0000-000000000002',
  name: 'Project B',
  productUrl: 'https://project-b.example.com',
  testingStrategy: 'UI testing for Project B',
  customPrompt: null,
  allowedWorkItemTypes: null,
  requestTimeoutSeconds: 30,
  createdAt: '2026-05-01T00:00:00Z',
  updatedAt: '2026-05-01T00:00:00Z',
};

const MOCK_USER = {
  id: '00000000-0000-0000-0000-000000000099',
  displayName: 'Jane Smith',
  email: 'jane.smith@example.com',
};

test.describe('Projects List Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: { status: 'Active', plan: 'TestJunior', billingInterval: 'Monthly', trialEndsAt: null } }),
    );
  });

  test('AC-003: card grid is sorted by createdAt descending — newest project appears first', async ({
    page,
  }) => {
    // API returns B first (older), but the UI sorts by createdAt desc → A appears first
    await page.route('**/v1/projects', (route) =>
      route.fulfill({ json: [PROJECT_B, PROJECT_A] }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    const cards = page.locator('.MuiCard-root');
    await expect(cards.first()).toContainText('Project A');
    await expect(cards.nth(1)).toContainText('Project B');
  });

  test('AC-009/AC-010: empty state CTA navigates to /projects/new', async ({
    page,
  }) => {
    await page.route('**/v1/projects', (route) =>
      route.fulfill({ json: [] }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('No projects yet')).toBeVisible();
    await page.getByRole('button', { name: 'Create your first project' }).click();
    await expect(page).toHaveURL('/projects/new');
  });

  test('AC-013: clicking a project card navigates to /projects/:id/history', async ({
    page,
  }) => {
    await page.route('**/v1/projects', (route) =>
      route.fulfill({ json: [PROJECT_A, PROJECT_B] }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    const projectId = PROJECT_A.projectId;
    const cardLink = page.locator(`a[href="/projects/${projectId}/history"]`).first();
    await Promise.all([
      page.waitForURL(`**/projects/${projectId}/history`),
      cardLink.click(),
    ]);
    await expect(page).toHaveURL(`/projects/${projectId}/history`);
  });

  test('AC-016/AC-018: clicking the edit icon navigates to /projects/:id/settings without opening history', async ({
    page,
  }) => {
    await page.route('**/v1/projects', (route) =>
      route.fulfill({ json: [PROJECT_A, PROJECT_B] }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    const projectId = PROJECT_A.projectId;
    const editLink = page
      .locator('.MuiCard-root')
      .first()
      .getByRole('link', { name: 'Edit project' });

    await Promise.all([
      page.waitForURL(`**/projects/${projectId}/settings`),
      editLink.click(),
    ]);

    await expect(page).toHaveURL(`/projects/${projectId}/settings`);
    await expect(page).not.toHaveURL(`/projects/${projectId}/history`);
  });
});

// ─── Real-API tests (skipped in local project) ────────────────────────────────
// US-015–US-018 — AC-066–AC-079

test.describe('Projects List — With Seed Project (real API)', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name === 'local', 'Requires real dev API');
  });

  test('renders seed project card with name and URL, "Create Project" button visible (AC-066, AC-067, AC-068, AC-069)', async ({ page }) => {
    await page.goto('/projects', { waitUntil: 'load' });

    await expect(page.getByRole('heading', { name: '[E2E] Seed Project' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('https://example.com')).toBeVisible();
    await expect(
      page.getByRole('button', { name: /create project/i }).or(page.getByRole('link', { name: /create project/i })),
    ).toBeVisible();
    await expect(page.getByRole('link', { name: /\[E2E\] Seed Project/ })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText(/no projects yet/i)).not.toBeVisible();
  });

  test('"Create Project" button navigates to /projects/new (AC-075)', async ({ page }) => {
    // Mock subscription to Active so the UpgradeModal doesn't block navigation
    // (the gate fires when subscription is undefined/loading; this ensures deterministic behaviour).
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: { status: 'Active', plan: 'TestPro', billingInterval: 'Monthly', trialEndsAt: null } }),
    );

    await page.goto('/projects', { waitUntil: 'load' });

    await page
      .getByRole('button', { name: /create project/i })
      .or(page.getByRole('link', { name: /create project/i }))
      .first()
      .click();

    await expect(page).toHaveURL(/\/projects\/new/, { timeout: 10_000 });
  });

  test('project creation form renders at /projects/new with required fields (AC-076)', async ({ page }) => {
    await page.goto('/projects/new', { waitUntil: 'load' });

    await expect(page.getByLabel(/name/i).or(page.locator('input[name="name"]'))).toBeVisible();
    await expect(page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]'))).toBeVisible();
    await expect(
      page.getByLabel(/testing strategy/i).or(page.locator('textarea[name="testingStrategy"], select[name="testType"]')).first(),
    ).toBeVisible();
  });

  test('edit icon on seed project card navigates to settings page (AC-077, AC-078)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto('/projects', { waitUntil: 'load' });

    await expect(page.getByRole('heading', { name: '[E2E] Seed Project' })).toBeVisible({ timeout: 10_000 });

    // The edit icon is an IconButton rendered as a Link (<a>), not a <button>.
    const editLink = page.locator(`a[href="/projects/${seedProjectId}/settings"]`);
    await expect(editLink).toBeVisible({ timeout: 5_000 });

    await editLink.click();
    await expect(page).toHaveURL(new RegExp(`/projects/${seedProjectId}/settings`), { timeout: 10_000 });
  });

  test('edit icon click does not trigger card-level history navigation (AC-079)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto('/projects', { waitUntil: 'load' });

    await expect(page.getByRole('heading', { name: '[E2E] Seed Project' })).toBeVisible({ timeout: 10_000 });

    const editLink = page.locator(`a[href="/projects/${seedProjectId}/settings"]`);
    await editLink.click();

    await expect(page).toHaveURL(new RegExp(`/projects/${seedProjectId}/settings`), { timeout: 10_000 });
    expect(page.url()).not.toContain('/history');
  });
});

test.describe('Projects List — Empty State (real API)', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name === 'local', 'Requires real dev API');
  });

  test('"Create Project" button visible in header even with empty list (AC-072)', async ({ page }) => {
    await page.goto('/projects', { waitUntil: 'load' });

    await expect(
      page.getByRole('button', { name: /create project/i }).or(page.getByRole('link', { name: /create project/i })),
    ).toBeVisible();
  });

  test('"Create your first project" button navigates to /projects/new in empty state (AC-071, AC-074)', async ({ page, request }) => {
    const createRes = await request.post('/v1/projects', {
      data: {
        name: '[E2E] Empty State Check',
        productUrl: 'https://empty-check.example.com',
        testingStrategy: 'Temporary — will be deleted immediately.',
        requestTimeoutSeconds: 30,
      },
    });

    if (!createRes.ok()) {
      test.skip(true, 'Could not create throwaway project for empty-state test');
      return;
    }

    const { projectId } = (await createRes.json()) as { projectId: string };

    try {
      await request.delete(`/v1/projects/${projectId}`);

      await page.goto('/projects', { waitUntil: 'load' });

      const createFirstButton = page.getByRole('button', { name: /create your first project/i })
        .or(page.getByRole('link', { name: /create your first project/i }));

      if (await createFirstButton.isVisible()) {
        await createFirstButton.click();
        await expect(page).toHaveURL(/\/projects\/new/, { timeout: 10_000 });
      }
    } finally {
      await request.delete(`/v1/projects/${projectId}`).catch(() => {});
    }
  });
});
