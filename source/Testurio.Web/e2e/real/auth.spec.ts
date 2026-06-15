/**
 * Auth specs — US-003 (Sign-In Happy Path), US-004 (Wrong Password), US-005 (Sign-Out)
 *
 * AC-010–AC-022
 *
 * NOTE: All page.goto() calls use waitUntil: 'load' (not 'networkidle').
 * The dashboard page opens a persistent SSE stream (EventSource) for live run
 * updates, which means 'networkidle' never resolves after landing on /dashboard.
 */

import { test, expect, Browser } from '@playwright/test';

// ---------------------------------------------------------------------------
// US-003 — Sign-In Happy Path
// ---------------------------------------------------------------------------

test.describe('Sign-In — Happy Path', () => {
  test('sign-in page renders form fields (AC-010, AC-013)', async ({ browser }: { browser: Browser }) => {
    // Use a fresh unauthenticated context so the sign-in form renders (not dashboard redirect).
    // Explicitly pass storageState: undefined to guarantee no cookies are inherited.
    const context = await browser.newContext({ storageState: undefined });
    const page = await context.newPage();

    await page.route('**/*', (route) =>
      route.continue({
        headers: {
          ...route.request().headers(),
          'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
          'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
        },
      }),
    );

    await page.goto('/sign-in', { waitUntil: 'load' });

    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign In' })).toBeVisible();

    // AC-013: sign-in page should NOT be wrapped in the shell layout
    await expect(page.locator('[data-testid="sidebar"]')).not.toBeVisible();

    await context.close();
  });

  test('valid credentials redirect to dashboard and show user identity (AC-011, AC-012)', async ({ browser }: { browser: Browser }) => {
    // Use a fresh context so this test is independent of the shared storageState.
    // Explicitly pass storageState: undefined to guarantee no cookies are inherited.
    const context = await browser.newContext({ storageState: undefined });
    const page = await context.newPage();

    // Inject CF headers for environments protected by Cloudflare Access
    await page.route('**/*', (route) =>
      route.continue({
        headers: {
          ...route.request().headers(),
          'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
          'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
        },
      }),
    );

    await page.goto('/sign-in', { waitUntil: 'load' });
    await page.locator('input[name="email"]').fill(process.env.TEST_USER_EMAIL!);
    await page.locator('input[name="password"]').fill(process.env.TEST_USER_PASSWORD!);
    await page.getByRole('button', { name: 'Sign In' }).click();

    await page.waitForURL('**/dashboard', { timeout: 30_000 });
    expect(page.url()).toContain('/dashboard');

    // AC-012: user display name or avatar is visible in the shell header.
    // AppHeader renders a <Typography> paragraph with the display name and an MUI Avatar
    // with role="img" aria-label={displayName}. No data-testid attributes are present,
    // so we select by the Avatar role or the display-name paragraph.
    const headerIdentity = page
      .getByRole('banner')
      .locator('p, [role="img"][aria-label]')
      .first();
    await expect(headerIdentity).toBeVisible({ timeout: 10_000 });

    await context.close();
  });

  test('already-authenticated user navigating to /sign-in is redirected to /dashboard (AC-014)', async ({ page }) => {
    // The shared storageState means this page context is already authenticated.
    // Use 'load' instead of 'networkidle': the dashboard opens an SSE stream
    // (EventSource) for live run updates, so 'networkidle' never resolves.
    await page.goto('/sign-in', { waitUntil: 'load' });
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 10_000 });
    await expect(page.locator('input[name="email"]')).not.toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// US-004 — Sign-In Wrong Password Error State
// ---------------------------------------------------------------------------

test.describe('Sign-In — Wrong Password', () => {
  test('invalid password shows inline error, no redirect, button re-enabled (AC-015, AC-016, AC-017)', async ({ browser }: { browser: Browser }) => {
    // Explicitly pass storageState: undefined to guarantee no cookies are inherited.
    const context = await browser.newContext({ storageState: undefined });
    const page = await context.newPage();

    await page.route('**/*', (route) =>
      route.continue({
        headers: {
          ...route.request().headers(),
          'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
          'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
        },
      }),
    );

    await page.goto('/sign-in', { waitUntil: 'load' });
    await page.locator('input[name="email"]').fill(process.env.TEST_USER_EMAIL!);
    await page.locator('input[name="password"]').fill('WrongPassword!1');
    await page.getByRole('button', { name: 'Sign In' }).click();

    // AC-015: inline error shown
    await expect(
      page.getByText(/incorrect email or password/i),
    ).toBeVisible({ timeout: 15_000 });

    // AC-017: still on /sign-in
    expect(page.url()).toContain('/sign-in');

    // AC-016: Sign In button is re-enabled
    await expect(page.getByRole('button', { name: 'Sign In' })).toBeEnabled();

    await context.close();
  });
});

// ---------------------------------------------------------------------------
// US-005 — Sign-Out Flow
// ---------------------------------------------------------------------------

test.describe('Sign-Out', () => {
  test('sign-out clears session and redirects to /sign-in (AC-018, AC-019, AC-020)', async ({ page }) => {
    // Use 'load' instead of 'networkidle': dashboard has a persistent SSE stream.
    await page.goto('/dashboard', { waitUntil: 'load' });

    // AC-021: sign-out button in sidebar shows loading state while signing out.
    // We find it first to assert it exists.
    const signOutButton = page.getByRole('button', { name: /sign out/i });
    await expect(signOutButton).toBeVisible();
    await signOutButton.click();

    // AC-019: after sign-out, browser should land on /sign-in or /
    await page.waitForURL(/\/(sign-in|$)/, { timeout: 30_000 });

    // AC-020: navigating to /dashboard after sign-out redirects to /sign-in
    await page.goto('/dashboard', { waitUntil: 'load' });
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });
  });
});
