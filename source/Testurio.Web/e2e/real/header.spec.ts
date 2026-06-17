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
      // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
      await page.goto(route, { waitUntil: 'load' });

      // AC-162, AC-163: header shows user identity on every authenticated page.
      // AppHeader renders the display name as a <Typography> (→ <p>) and an <Avatar>
      // inside the banner. There are no data-testid attributes on these elements.
      // We locate the banner's first <p> (the display name text) as the identity signal.
      await expect(
        page.getByRole('banner').locator('p').first(),
      ).toBeVisible({ timeout: 10_000 });
    }
  });

  test('Testurio logo in header navigates to /dashboard (AC-164)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    // AppHeader renders the logo as:
    //   <Box component={Link} href="/dashboard" aria-label="TestUR.io — go to dashboard">
    //   <Typography>TestUR.io</Typography>
    // Note: the brand name is "TestUR.io" — the dot between "TestUR" and "io" means
    // /testurio/i does NOT match (it would need the letters to be contiguous).
    // Instead we match on the "go to dashboard" part of the aria-label, or target
    // the link by its href="/dashboard" inside the banner.
    const logo = page
      .getByRole('banner')
      .getByRole('link', { name: /go to dashboard/i })
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
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    // AppSidebar renders the toggle as an <IconButton> with aria-label from i18n:
    //   "Collapse sidebar" (expanded) / "Expand sidebar" (collapsed).
    // [aria-label*="collapse"] matches "Collapse sidebar" (case-insensitive via icontains
    // is not supported, so we rely on the capitalised value in the label).
    await expect(
      page.getByRole('button', { name: /collapse sidebar|expand sidebar/i }).first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('clicking toggle collapses sidebar to icon-only width and hides labels (AC-166)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    const toggle = page.getByRole('button', { name: /collapse sidebar|expand sidebar/i }).first();

    // AppSidebar uses a MUI Drawer whose Paper element carries the actual pixel width.
    // EXPANDED_WIDTH = 240px, COLLAPSED_WIDTH = 64px with 200ms CSS transition.
    const sidebar = page.locator('.MuiDrawer-paper').first();

    // Ensure the sidebar starts expanded
    await expect(sidebar).toBeVisible({ timeout: 10_000 });

    await toggle.click();

    // AC-166: sidebar collapses — width reduces to icon-only (64px).
    // Poll to allow for the 200ms CSS transition to finish.
    await expect.poll(async () => {
      const box = await sidebar.boundingBox();
      return box?.width ?? 9999;
    }, { timeout: 5_000 }).toBeLessThanOrEqual(80);

    // Navigation labels hidden when collapsed.
    // When collapsed, AppSidebar does NOT render <ListItemText> — only the icon is shown.
    // The link <a> element stays visible (icon-only) but carries an aria-label instead
    // of visible text. Verify the link has no visible text content.
    const dashLink = page.getByRole('link', { name: /^dashboard$/i }).first();
    const textContent = await dashLink.textContent();
    expect(textContent?.trim()).toBe('');
  });

  test('clicking toggle again expands sidebar back to full width (AC-167)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    const toggle = page.getByRole('button', { name: /collapse sidebar|expand sidebar/i }).first();
    const sidebar = page.locator('.MuiDrawer-paper').first();

    await expect(sidebar).toBeVisible({ timeout: 10_000 });

    // Collapse
    await toggle.click();
    await expect.poll(async () => {
      const box = await sidebar.boundingBox();
      return box?.width ?? 9999;
    }, { timeout: 5_000 }).toBeLessThanOrEqual(80);

    // Expand
    await toggle.click();
    await expect.poll(async () => {
      const box = await sidebar.boundingBox();
      return box?.width ?? 0;
    }, { timeout: 5_000 }).toBeGreaterThanOrEqual(200);

    // Navigation labels visible again — expanded sidebar renders <ListItemText>
    // so the link element has non-empty text content.
    const dashLinkExpanded = page.getByRole('link', { name: /^dashboard$/i }).first();
    await expect(dashLinkExpanded).toBeVisible();
    const expandedText = await dashLinkExpanded.textContent();
    expect(expandedText?.trim().length).toBeGreaterThan(0);
  });

  test('collapsed state persists in localStorage across reload (AC-168)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/dashboard', { waitUntil: 'load' });

    const toggle = page.getByRole('button', { name: /collapse sidebar|expand sidebar/i }).first();

    // Ensure sidebar starts expanded before we collapse it
    const sidebar = page.locator('.MuiDrawer-paper').first();
    await expect(sidebar).toBeVisible({ timeout: 10_000 });

    // Ensure it's currently expanded (width ≥ 200px); if already collapsed, expand first
    const initialBox = await sidebar.boundingBox();
    if ((initialBox?.width ?? 0) <= 80) {
      await toggle.click();
      await expect.poll(async () => {
        const box = await sidebar.boundingBox();
        return box?.width ?? 0;
      }, { timeout: 5_000 }).toBeGreaterThanOrEqual(200);
    }

    // Collapse the sidebar
    await toggle.click();
    await expect.poll(async () => {
      const box = await sidebar.boundingBox();
      return box?.width ?? 9999;
    }, { timeout: 5_000 }).toBeLessThanOrEqual(80);

    // Verify localStorage key is set — useSidebarState writes 'true' as a string
    const collapsed = await page.evaluate(() =>
      localStorage.getItem('testurio.sidebarCollapsed'),
    );
    expect(collapsed).toBe('true');

    // Reload and verify sidebar remains collapsed (localStorage value restored by useEffect)
    // waitUntil: 'load' — SSE stream prevents networkidle.
    await page.reload({ waitUntil: 'load' });

    await expect.poll(async () => {
      const box = await sidebar.boundingBox();
      return box?.width ?? 9999;
    }, { timeout: 5_000 }).toBeLessThanOrEqual(80);

    // Restore expanded state so other tests aren't affected
    await page.getByRole('button', { name: /collapse sidebar|expand sidebar/i }).first().click();
    await expect.poll(async () => {
      const box = await sidebar.boundingBox();
      return box?.width ?? 0;
    }, { timeout: 5_000 }).toBeGreaterThanOrEqual(200);
  });
});
