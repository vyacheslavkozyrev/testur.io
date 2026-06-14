/**
 * Header and Sidebar Collapse specs — US-038 (Header Identity), US-039 (Collapse/Expand)
 *
 * AC-162–AC-168
 */

import { test, expect } from '@playwright/test';

// ---------------------------------------------------------------------------
// US-038 — Header — User Display Name Shown
// ---------------------------------------------------------------------------

test.describe('Portal Header — User Identity', () => {
  test('user display name/avatar visible on /dashboard, /projects, and /settings (AC-162, AC-163)', async ({ page }) => {
    const pages = ['/dashboard', '/projects', '/settings'];

    for (const route of pages) {
      await page.goto(route, { waitUntil: 'networkidle' });

      // AC-162, AC-163: header shows user identity on every authenticated page
      await expect(
        page
          .getByRole('banner')
          .locator('[data-testid="user-identity"], [data-testid="user-avatar"], [aria-label*="account"], [aria-label*="user"]')
          .first(),
      ).toBeVisible({ timeout: 10_000 });
    }
  });

  test('Testurio logo in header navigates to /dashboard (AC-164)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    // Click the logo (image or branded link in the header)
    const logo = page
      .getByRole('banner')
      .getByRole('link', { name: /testurio|home/i })
      .or(page.getByRole('banner').locator('[data-testid="logo-link"]'))
      .first();

    await logo.click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
  });
});

// ---------------------------------------------------------------------------
// US-039 — Sidebar — Collapse and Expand
// ---------------------------------------------------------------------------

test.describe('Sidebar — Collapse and Expand', () => {
  test('collapse toggle button is visible (AC-165)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    await expect(
      page.locator('[data-testid="sidebar-toggle"], [aria-label*="collapse"], [aria-label*="toggle sidebar"]').first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('clicking toggle collapses sidebar to icon-only width and hides labels (AC-166)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    const toggle = page
      .locator('[data-testid="sidebar-toggle"], [aria-label*="collapse"], [aria-label*="toggle sidebar"]')
      .first();

    // Ensure sidebar is expanded first
    const sidebar = page.locator('[data-testid="sidebar"]').first();

    await toggle.click();

    // AC-166: sidebar collapses — width reduces
    const sidebarBox = await sidebar.boundingBox();
    // When collapsed, width should be ≤ 80px (icon-only)
    expect(sidebarBox?.width).toBeLessThanOrEqual(80);

    // Navigation labels hidden
    await expect(page.getByRole('link', { name: /^dashboard$/i })).not.toBeVisible();
  });

  test('clicking toggle again expands sidebar back to full width (AC-167)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    const toggle = page
      .locator('[data-testid="sidebar-toggle"], [aria-label*="collapse"], [aria-label*="toggle sidebar"]')
      .first();
    const sidebar = page.locator('[data-testid="sidebar"]').first();

    // Collapse
    await toggle.click();
    const collapsedBox = await sidebar.boundingBox();
    expect(collapsedBox?.width).toBeLessThanOrEqual(80);

    // Expand
    await toggle.click();
    const expandedBox = await sidebar.boundingBox();
    // Full width should be ≥ 200px
    expect(expandedBox?.width).toBeGreaterThanOrEqual(200);

    // Navigation labels visible again
    await expect(page.getByRole('link', { name: /^dashboard$/i })).toBeVisible();
  });

  test('collapsed state persists in localStorage across reload (AC-168)', async ({ page }) => {
    await page.goto('/dashboard', { waitUntil: 'networkidle' });

    const toggle = page
      .locator('[data-testid="sidebar-toggle"], [aria-label*="collapse"], [aria-label*="toggle sidebar"]')
      .first();

    // Collapse the sidebar
    await toggle.click();

    // Verify localStorage key is set
    const collapsed = await page.evaluate(() =>
      localStorage.getItem('testurio.sidebarCollapsed'),
    );
    expect(collapsed).toBeTruthy();
    expect(['true', '1', 'collapsed']).toContain(collapsed);

    // Reload and verify sidebar remains collapsed
    await page.reload({ waitUntil: 'networkidle' });

    const sidebar = page.locator('[data-testid="sidebar"]').first();
    const sidebarBox = await sidebar.boundingBox();
    expect(sidebarBox?.width).toBeLessThanOrEqual(80);

    // Restore expanded state so other tests aren't affected
    await toggle.click();
  });
});
