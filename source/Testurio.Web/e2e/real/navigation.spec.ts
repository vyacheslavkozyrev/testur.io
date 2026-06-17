/**
 * Sidebar Navigation specs — US-036 (Links Navigate), US-037 (Active Item)
 *
 * AC-154–AC-161
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
// US-036 — Sidebar Links Navigate Correctly
// ---------------------------------------------------------------------------

test.describe('Sidebar Navigation — Links', () => {
  test('Dashboard link navigates to /dashboard (AC-154)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    // Wait for the sidebar to be visible
    await expect(page.getByRole('link', { name: /^dashboard$/i })).toBeVisible({ timeout: 10_000 });

    await page.getByRole('link', { name: /^dashboard$/i }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });

    // AC-154: navigation completed — URL changed to /dashboard.
    // Note: Playwright's framenavigated fires once even for Next.js client-side
    // transitions (App Router uses history.pushState but Playwright intercepts it).
    // We validate the URL change above rather than counting navigation events.
  });

  test('Projects link navigates to /projects (AC-155)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    await expect(page.getByRole('link', { name: /^projects$/i })).toBeVisible({ timeout: 10_000 });

    await page.getByRole('link', { name: /^projects$/i }).click();
    await expect(page).toHaveURL(/\/projects/, { timeout: 10_000 });
  });

  test('Settings link navigates to /settings (AC-156)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    await expect(page.getByRole('link', { name: /^settings$/i })).toBeVisible({ timeout: 10_000 });

    await page.getByRole('link', { name: /^settings$/i }).click();
    await expect(page).toHaveURL(/\/settings/, { timeout: 10_000 });
  });
});

// ---------------------------------------------------------------------------
// US-037 — Active Sidebar Item Highlighted
// ---------------------------------------------------------------------------

test.describe('Sidebar Navigation — Active Highlight', () => {
  test('Dashboard link has active style when on /dashboard (AC-158)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    // AppSidebar uses MUI ListItemButton with component={Link} — when selected=true,
    // MUI adds the "Mui-selected" CSS class directly to the rendered <a> element.
    // Playwright's page snapshot shows the active link as [active].
    const dashboardLink = page.getByRole('link', { name: /^dashboard$/i });
    await expect(dashboardLink).toBeVisible({ timeout: 10_000 });

    const classAttr = await dashboardLink.getAttribute('class') ?? '';
    // MUI ListItemButton selected state adds Mui-selected class
    expect(classAttr).toMatch(/Mui-selected/);

    // Projects link is NOT active on /dashboard
    const projectsLink = page.getByRole('link', { name: /^projects$/i });
    const projectsClass = await projectsLink.getAttribute('class') ?? '';
    expect(projectsClass).not.toMatch(/Mui-selected/);
  });

  test('Projects link has active style when on /projects (AC-159)', async ({ page }) => {
    await page.goto('/projects', { waitUntil: 'load' });

    const projectsLink = page.getByRole('link', { name: /^projects$/i });
    await expect(projectsLink).toBeVisible({ timeout: 10_000 });

    const classAttr = await projectsLink.getAttribute('class') ?? '';
    expect(classAttr).toMatch(/Mui-selected/);
  });

  test('Settings link has active style when on /settings (AC-160)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'load' });

    const settingsLink = page.getByRole('link', { name: /^settings$/i });
    await expect(settingsLink).toBeVisible({ timeout: 10_000 });

    const classAttr = await settingsLink.getAttribute('class') ?? '';
    expect(classAttr).toMatch(/Mui-selected/);
  });

  test('Projects link has active style on /projects/:id/history (prefix match) (AC-161)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/history`, { waitUntil: 'load' });

    const projectsLink = page.getByRole('link', { name: /^projects$/i });
    await expect(projectsLink).toBeVisible({ timeout: 10_000 });

    const classAttr = await projectsLink.getAttribute('class') ?? '';
    expect(classAttr).toMatch(/Mui-selected/);
  });
});
