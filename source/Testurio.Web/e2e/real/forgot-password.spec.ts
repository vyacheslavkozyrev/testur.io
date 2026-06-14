/**
 * Forgot Password specs — US-009
 *
 * AC-038–AC-044
 *
 * The test navigates to /forgot-password and verifies the page + confirmation
 * message. It does NOT complete the actual B2C email-based password-reset flow
 * (that would require a real mailbox).
 *
 * Tests that require interacting with a live email inbox are tagged @slow and
 * only run when RUN_SLOW_TESTS=true is set in the environment.
 */

import { test, expect, Browser } from '@playwright/test';

// Helper: open a fresh (unauthenticated) page with CF headers injected
async function openUnauthPage(browser: Browser) {
  const context = await browser.newContext();
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

test.describe('Forgot Password', () => {
  test('"Forgot password?" link on sign-in navigates to /forgot-password (AC-038)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-in', { waitUntil: 'networkidle' });

    await page.getByRole('link', { name: /forgot password/i }).click();

    await expect(page).toHaveURL(/\/forgot-password/, { timeout: 10_000 });

    await context.close();
  });

  test('forgot-password page renders Email field and submit button, not in shell layout (AC-039, AC-043)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'networkidle' });

    // AC-039: Email field and submit button present
    await expect(page.locator('input[name="email"], input[type="email"]').first()).toBeVisible();
    await expect(page.getByRole('button', { name: /send reset link/i })).toBeVisible();

    // AC-043: page is not in the shell layout (no sidebar)
    await expect(page.locator('[data-testid="sidebar"]')).not.toBeVisible();

    await context.close();
  });

  test('submitting a valid email shows confirmation message (AC-040, AC-042)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'networkidle' });

    await page.locator('input[name="email"], input[type="email"]').first().fill(process.env.TEST_USER_EMAIL!);
    await page.getByRole('button', { name: /send reset link/i }).click();

    // AC-040: confirmation message shown
    await expect(
      page.getByText(/if an account exists for that email, a reset link has been sent/i),
    ).toBeVisible({ timeout: 15_000 });

    await context.close();
  });

  test('confirmation message shown for unregistered email too (AC-042)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'networkidle' });

    // Use an email that definitely is not registered
    await page.locator('input[name="email"], input[type="email"]').first().fill(`no-such-account-${Date.now()}@testur.io`);
    await page.getByRole('button', { name: /send reset link/i }).click();

    // AC-042: same confirmation regardless of whether account exists (no enumeration)
    await expect(
      page.getByText(/if an account exists for that email, a reset link has been sent/i),
    ).toBeVisible({ timeout: 15_000 });

    await context.close();
  });

  test('"Back to sign in" link on confirmation page navigates to /sign-in (AC-041)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'networkidle' });
    await page.locator('input[name="email"], input[type="email"]').first().fill(process.env.TEST_USER_EMAIL!);
    await page.getByRole('button', { name: /send reset link/i }).click();

    await expect(
      page.getByText(/if an account exists for that email, a reset link has been sent/i),
    ).toBeVisible({ timeout: 15_000 });

    // AC-041: "Back to sign in" link present and navigates to /sign-in
    await page.getByRole('link', { name: /back to sign in/i }).click();
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    await context.close();
  });

  test('submitting with empty email shows validation error and form is not submitted (AC-044)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'networkidle' });

    await page.getByRole('button', { name: /send reset link/i }).click();

    // AC-044: inline validation error appears, no confirmation message
    await expect(
      page.getByText(/email is required|please enter your email|enter a valid email/i),
    ).toBeVisible({ timeout: 5_000 });

    await expect(
      page.getByText(/if an account exists for that email, a reset link has been sent/i),
    ).not.toBeVisible();

    await context.close();
  });
});
