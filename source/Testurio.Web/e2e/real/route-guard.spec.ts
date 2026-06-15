/**
 * Route Guard specs — US-006
 *
 * AC-023–AC-027
 *
 * Each test uses a completely fresh browser context with no stored auth state
 * so that every navigation is truly unauthenticated.
 *
 * NOTE: All page.goto() calls use waitUntil: 'load' (not 'networkidle').
 * Authenticated pages open a persistent SSE stream (EventSource), so 'networkidle'
 * never resolves after a redirect lands on /dashboard or any authenticated page.
 */

import { test, expect, Browser } from '@playwright/test';

// Helper: open a fresh (unauthenticated) page with CF headers injected.
// storageState: undefined ensures no cookies are carried over from the
// project-level storageState that the chromium project uses for authenticated tests.
async function openUnauthPage(browser: Browser) {
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

  return { context, page };
}

test.describe('Route Guard — Unauthenticated Redirects', () => {
  test('/dashboard redirects to /sign-in with returnUrl param (AC-023, AC-024)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/dashboard', { waitUntil: 'load' });
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    // AC-024: returnUrl param preserving originally requested path
    expect(page.url()).toContain('returnUrl');
    expect(decodeURIComponent(page.url())).toContain('/dashboard');

    await context.close();
  });

  test('/projects redirects to /sign-in (AC-025)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/projects', { waitUntil: 'load' });
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    await context.close();
  });

  test('/settings redirects to /sign-in (AC-026)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/settings', { waitUntil: 'load' });
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    await context.close();
  });

  test('/projects/:id/settings redirects to /sign-in (AC-027)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    // Use a placeholder project ID — the guard fires before the API is called
    await page.goto('/projects/00000000-0000-0000-0000-000000000000/settings', { waitUntil: 'load' });
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    await context.close();
  });
});
