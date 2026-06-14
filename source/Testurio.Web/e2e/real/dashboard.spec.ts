/**
 * Dashboard specs — US-013 (Dashboard Overview), US-014 (Card Navigation)
 *
 * AC-059–AC-065
 */

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '../.auth/seed.json');

function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

// ---------------------------------------------------------------------------
// US-013 — Dashboard Overview Page Loads
// ---------------------------------------------------------------------------

test.describe('Dashboard', () => {
  test('dashboard page loads without error and shows required elements (AC-059, AC-060, AC-062, AC-063)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle', timeout: 15_000 });

    // AC-059: no error boundary or HTTP error
    expect(page.url()).toContain('/dashboard');
    await expect(page.getByRole('heading', { name: /dashboard/i }).or(page.locator('[data-testid="dashboard-page"]'))).toBeVisible({ timeout: 5_000 });

    // AC-060: "Create Project" button in dashboard header
    await expect(page.getByRole('button', { name: /create project/i }).or(page.getByRole('link', { name: /create project/i }))).toBeVisible();

    // AC-062: quota usage indicator visible
    await expect(
      page.locator('[data-testid="quota-usage"], [data-testid="quota-bar"]').or(
        page.getByText(/runs|no active plan/i).first(),
      ),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('seed project card is visible with name and run status badge (AC-061, AC-063)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    // AC-061: seed project card with correct name
    const seedCard = page.getByText('[E2E] Seed Project').first();
    await expect(seedCard).toBeVisible({ timeout: 10_000 });

    // AC-063: run status badge visible on the card (any status including never_run)
    const card = page.locator('[data-testid="project-card"]', { hasText: '[E2E] Seed Project' });
    await expect(card.locator('[data-testid="run-status-badge"], [class*="badge"], [class*="status"]').first()).toBeVisible();
  });

  // ---------------------------------------------------------------------------
  // US-014 — Dashboard Card Navigates to Project History
  // ---------------------------------------------------------------------------

  test('clicking seed project card navigates to history page (AC-064)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    const card = page
      .locator('[data-testid="project-card"]', { hasText: '[E2E] Seed Project' })
      .or(page.locator('[class*="card"]', { hasText: '[E2E] Seed Project' }))
      .first();

    await card.click();

    await page.waitForURL(`**/projects/${seedProjectId}/history`, { timeout: 10_000 });
    expect(page.url()).toContain(`/projects/${seedProjectId}/history`);
  });

  test('Back button from history returns to dashboard (AC-065)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    // Navigate to history
    const card = page
      .locator('[data-testid="project-card"]', { hasText: '[E2E] Seed Project' })
      .or(page.locator('[class*="card"]', { hasText: '[E2E] Seed Project' }))
      .first();
    await card.click();
    await page.waitForURL(`**/projects/${seedProjectId}/history`, { timeout: 10_000 });

    // AC-065: browser Back returns to /dashboard without full reload
    await page.goBack();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
  });
});
