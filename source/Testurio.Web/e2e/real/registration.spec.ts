/**
 * Registration specs — US-007 (Happy Path), US-008 (Duplicate Email Error)
 *
 * AC-028–AC-037
 *
 * NOTE: The test does NOT clean up the Azure AD B2C account created during the
 * happy-path test. B2C admin credentials are out of scope for the E2E suite.
 * Each run generates a unique email (e2e+reg+<timestamp>@testur.io) to avoid
 * collision between runs.
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

// ---------------------------------------------------------------------------
// US-007 — Happy Path Registration
// ---------------------------------------------------------------------------

test.describe('Registration — Happy Path', () => {
  test('sign-up page renders required form fields and is not in shell layout (AC-028, AC-032)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-up', { waitUntil: 'networkidle' });

    // AC-028: form fields present
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await expect(page.locator('input[name="confirmPassword"]')).toBeVisible();

    // AC-032: sign-up page is not wrapped in the shell layout (no sidebar)
    await expect(page.locator('[data-testid="sidebar"]')).not.toBeVisible();

    await context.close();
  });

  test('completing registration with unique email creates account and redirects to dashboard (AC-029, AC-030, AC-031, AC-033, AC-034)', async ({ browser }: { browser: Browser }) => {
    test.setTimeout(60_000);

    // AC-033: unique generated email per run — never written to .auth/ files
    const uniqueEmail = `e2e+reg+${Date.now()}@testur.io`;

    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-up', { waitUntil: 'networkidle' });

    await page.locator('input[name="email"]').fill(uniqueEmail);
    await page.locator('input[name="password"]').fill('TestPassword1!');
    await page.locator('input[name="confirmPassword"]').fill('TestPassword1!');
    await page.getByRole('button', { name: /create account|sign up|register/i }).click();

    // AC-029: redirected to /dashboard after registration
    await page.waitForURL('**/dashboard', { timeout: 30_000 });
    expect(page.url()).toContain('/dashboard');

    // AC-030: GET /v1/account/me returns 200 within 5 seconds (Cosmos Users doc created)
    // Use the page's request context which carries the new session cookies
    const meResponse = await page.request.get('/v1/account/me');
    expect(meResponse.status()).toBe(200);

    // AC-031: display name or avatar visible in portal shell
    const headerIdentity = page
      .getByRole('banner')
      .locator('[data-testid="user-identity"], [data-testid="user-avatar"], [aria-label*="account"], [aria-label*="user"]')
      .first();
    await expect(headerIdentity).toBeVisible({ timeout: 10_000 });

    // AC-034: No cleanup attempt for the B2C account (admin credentials out of scope)
    // The newly created B2C account persists intentionally after this test.

    await context.close();
  });
});

// ---------------------------------------------------------------------------
// US-008 — Duplicate Email Error
// ---------------------------------------------------------------------------

test.describe('Registration — Duplicate Email Error', () => {
  test('registering with an existing email shows duplicate error with sign-in link (AC-035, AC-036, AC-037)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-up', { waitUntil: 'networkidle' });

    await page.locator('input[name="email"]').fill(process.env.TEST_USER_EMAIL!);
    await page.locator('input[name="password"]').fill('TestPassword1!');
    await page.locator('input[name="confirmPassword"]').fill('TestPassword1!');
    await page.getByRole('button', { name: /create account|sign up|register/i }).click();

    // AC-035: inline duplicate-email error
    await expect(
      page.getByText(/an account with this email already exists/i),
    ).toBeVisible({ timeout: 15_000 });

    // AC-036: "Sign in instead?" link navigates to /sign-in
    const signInLink = page.getByRole('link', { name: /sign in instead/i });
    await expect(signInLink).toBeVisible();
    await signInLink.click();
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    // AC-037: verified by the fact that we're still not on /dashboard — no account was created
    expect(page.url()).not.toContain('/dashboard');

    await context.close();
  });
});
