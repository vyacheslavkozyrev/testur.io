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
    await page.goto('/settings', { waitUntil: 'networkidle' });

    let reloaded = false;
    page.on('load', () => { reloaded = true; });
    reloaded = false;

    await page.getByRole('link', { name: /^dashboard$/i }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });

    // AC-157: client-side navigation (no full reload)
    expect(reloaded).toBe(false);
  });

  test('Projects link navigates to /projects (AC-155)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    let reloaded = false;
    page.on('load', () => { reloaded = true; });
    reloaded = false;

    await page.getByRole('link', { name: /^projects$/i }).click();
    await expect(page).toHaveURL(/\/projects/, { timeout: 10_000 });

    expect(reloaded).toBe(false);
  });

  test('Settings link navigates to /settings (AC-156)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    let reloaded = false;
    page.on('load', () => { reloaded = true; });
    reloaded = false;

    await page.getByRole('link', { name: /^settings$/i }).click();
    await expect(page).toHaveURL(/\/settings/, { timeout: 10_000 });

    expect(reloaded).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// US-037 — Active Sidebar Item Highlighted
// ---------------------------------------------------------------------------

test.describe('Sidebar Navigation — Active Highlight', () => {
  test('Dashboard link has active style when on /dashboard (AC-158)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    const dashboardLink = page.getByRole('link', { name: /^dashboard$/i });
    const classOrAria =
      (await dashboardLink.getAttribute('class')) +
      (await dashboardLink.getAttribute('aria-current') ?? '');

    expect(classOrAria).toMatch(/active|selected|current/);

    // Projects and Settings are NOT active
    const projectsLink = page.getByRole('link', { name: /^projects$/i });
    const projectsClassOrAria =
      (await projectsLink.getAttribute('class') ?? '') +
      (await projectsLink.getAttribute('aria-current') ?? '');
    expect(projectsClassOrAria).not.toMatch(/\bactive\b|\bselected\b|\bcurrent\b/);
  });

  test('Projects link has active style when on /projects (AC-159)', async ({ page }) => {
    await page.goto('/projects', { waitUntil: 'networkidle' });

    const projectsLink = page.getByRole('link', { name: /^projects$/i });
    const classOrAria =
      (await projectsLink.getAttribute('class') ?? '') +
      (await projectsLink.getAttribute('aria-current') ?? '');
    expect(classOrAria).toMatch(/active|selected|current/);
  });

  test('Settings link has active style when on /settings (AC-160)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    const settingsLink = page.getByRole('link', { name: /^settings$/i });
    const classOrAria =
      (await settingsLink.getAttribute('class') ?? '') +
      (await settingsLink.getAttribute('aria-current') ?? '');
    expect(classOrAria).toMatch(/active|selected|current/);
  });

  test('Projects link has active style on /projects/:id/history (prefix match) (AC-161)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/history`, { waitUntil: 'networkidle' });

    const projectsLink = page.getByRole('link', { name: /^projects$/i });
    const classOrAria =
      (await projectsLink.getAttribute('class') ?? '') +
      (await projectsLink.getAttribute('aria-current') ?? '');
    expect(classOrAria).toMatch(/active|selected|current/);
  });
});
