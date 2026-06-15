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
    // waitUntil: 'load' — 'networkidle' times out because the SSE stream
    // (/v1/dashboard/stream) keeps the connection open indefinitely.
    await page.goto('/dashboard', { waitUntil: 'load', timeout: 15_000 });

    // AC-059: no error boundary or HTTP error
    expect(page.url()).toContain('/dashboard');
    // DashboardPage renders an h5 with t('page.title') = "Dashboard"
    await expect(page.getByRole('heading', { name: /dashboard/i }).first()).toBeVisible({ timeout: 10_000 });

    // AC-060: "Create Project" button in dashboard header
    await expect(
      page.getByRole('button', { name: /create project/i }).or(page.getByRole('link', { name: /create project/i })),
    ).toBeVisible();

    // AC-062: quota usage indicator visible.
    // QuotaUsageBar renders text like "X runs used this month" or "No active plan".
    await expect(
      page.getByText(/runs used this month|no active plan/i).first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('seed project card is visible with name and run status badge (AC-061, AC-063)', async ({ page }) => {
    // waitUntil: 'load' — see note above about SSE stream.
    await page.goto('/dashboard', { waitUntil: 'load' });

    // AC-061: seed project card with correct name.
    // ProjectCard renders as a CardActionArea (Link) — the heading is h6.
    await expect(page.getByRole('heading', { name: '[E2E] Seed Project' })).toBeVisible({ timeout: 10_000 });

    // AC-063: run status badge visible on the card.
    // RunStatusBadge renders as a MUI Chip — no data-testid. Chip is a <div role="status">
    // or just a <span>. The label text for NeverRun is "Never run".
    // Use the link that wraps the card and find the Chip by its label text.
    const cardLink = page.getByRole('link', { name: /\[E2E\] Seed Project/ });
    await expect(
      cardLink.getByText(/never run|queued|running|passed|failed|cancelled|timed out/i).first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  // ---------------------------------------------------------------------------
  // US-014 — Dashboard Card Navigates to Project History
  // ---------------------------------------------------------------------------

  test('clicking seed project card navigates to history page (AC-064)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    // waitUntil: 'load' — see note above about SSE stream.
    await page.goto('/dashboard', { waitUntil: 'load' });

    // ProjectCard renders as a CardActionArea (Link) navigating to /projects/:id/history.
    // Wait for the heading to appear before clicking.
    await expect(page.getByRole('heading', { name: '[E2E] Seed Project' })).toBeVisible({ timeout: 10_000 });

    const cardLink = page.getByRole('link', { name: /\[E2E\] Seed Project/ });
    await cardLink.click();

    await page.waitForURL(`**/projects/${seedProjectId}/history`, { timeout: 10_000 });
    expect(page.url()).toContain(`/projects/${seedProjectId}/history`);
  });

  test('Back button from history returns to dashboard (AC-065)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    // waitUntil: 'load' — see note above about SSE stream.
    await page.goto('/dashboard', { waitUntil: 'load' });

    // Wait for the card to be visible before clicking.
    await expect(page.getByRole('heading', { name: '[E2E] Seed Project' })).toBeVisible({ timeout: 10_000 });

    // Navigate to history via the card link
    const cardLink = page.getByRole('link', { name: /\[E2E\] Seed Project/ });
    await cardLink.click();
    await page.waitForURL(`**/projects/${seedProjectId}/history`, { timeout: 10_000 });

    // AC-065: browser Back returns to /dashboard without full reload
    await page.goBack();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
  });
});
