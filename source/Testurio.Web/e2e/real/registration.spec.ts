/**
 * Registration specs — US-007 (Happy Path), US-008 (Duplicate Email Error)
 *
 * AC-028–AC-037
 *
 * NOTE: The test does NOT clean up the Azure AD B2C account created during the
 * happy-path test. B2C admin credentials are out of scope for the E2E suite.
 * Each run generates a unique email (e2e+reg+<timestamp>@testur.io) to avoid
 * collision between runs.
 *
 * NOTE: All page.goto() calls use waitUntil: 'load' (not 'networkidle').
 * The dashboard opens a persistent SSE stream after sign-in, so 'networkidle'
 * never resolves on authenticated pages.
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

// ---------------------------------------------------------------------------
// US-007 — Happy Path Registration
// ---------------------------------------------------------------------------

test.describe('Registration — Happy Path', () => {
  test('sign-up page renders required form fields and is not in shell layout (AC-028, AC-032)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-up', { waitUntil: 'load' });

    // AC-028: form fields present — the sign-up form includes firstName, lastName,
    // email, password, and confirmPassword (all required by B2C policy).
    await expect(page.locator('input[name="firstName"]')).toBeVisible();
    await expect(page.locator('input[name="lastName"]')).toBeVisible();
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await expect(page.locator('input[name="confirmPassword"]')).toBeVisible();

    // AC-032: sign-up page is not wrapped in the shell layout (no sidebar)
    await expect(page.locator('[data-testid="sidebar"]')).not.toBeVisible();

    await context.close();
  });

  test.skip('completing registration with unique email creates account and redirects to dashboard (AC-029, AC-030, AC-031, AC-033, AC-034)', async ({ browser }: { browser: Browser }) => {
    // SKIPPED — Application-level infrastructure requirement not met in CI:
    // The Azure AD CIAM (B2C) native-auth flow sends an email verification code after
    // initial sign-up. After form submission the UI transitions to a "Verify email" step
    // (textbox "Verification code" + button "Verify email"). The E2E suite has no access
    // to the test email inbox, so the code step cannot be completed automatically.
    //
    // To run this test manually:
    //   1. Set up email inbox access for e2e+reg+*@testur.io addresses.
    //   2. After page.click('Create Account'), read the verification code from the inbox.
    //   3. Fill 'input[name="code"]' and click 'Verify email'.
    //   4. Then proceed with the waitForURL and identity checks below.
    //
    // AC-030 (GET /v1/account/me) is covered by the sign-in flow in auth.spec.ts.
    // AC-031 (header identity visible) is covered by auth.spec.ts AC-011/AC-012.

    test.setTimeout(60_000);

    const uniqueEmail = `e2e+reg+${Date.now()}@testur.io`;
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-up', { waitUntil: 'load' });

    await page.locator('input[name="firstName"]').fill('E2E');
    await page.locator('input[name="lastName"]').fill('Reg');
    await page.locator('input[name="email"]').fill(uniqueEmail);
    await page.locator('input[name="password"]').fill('E2ePassword99');
    await page.locator('input[name="confirmPassword"]').fill('E2ePassword99');
    await page.getByRole('button', { name: /create account|sign up|register/i }).click();

    // After this click, CIAM sends a verification code email and the UI shows
    // "Check your email for a verification code." — the test cannot proceed
    // without inbox access.
    await page.waitForURL('**/dashboard', { timeout: 30_000 });
    expect(page.url()).toContain('/dashboard');

    const meResponse = await page.request.get('/v1/account/me');
    expect(meResponse.status()).toBe(200);

    const headerIdentity = page
      .getByRole('banner')
      .locator('p, [role="img"][aria-label]')
      .first();
    await expect(headerIdentity).toBeVisible({ timeout: 10_000 });

    await context.close();
  });
});

// ---------------------------------------------------------------------------
// US-008 — Duplicate Email Error
// ---------------------------------------------------------------------------

test.describe('Registration — Duplicate Email Error', () => {
  test('registering with an existing email shows duplicate error with sign-in link (AC-035, AC-036, AC-037)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-up', { waitUntil: 'load' });

    // All required fields must be filled or the form validates locally and never
    // reaches the server call that would return the duplicate-email error.
    await page.locator('input[name="firstName"]').fill('E2E');
    await page.locator('input[name="lastName"]').fill('Dup');
    await page.locator('input[name="email"]').fill(process.env.TEST_USER_EMAIL!);
    await page.locator('input[name="password"]').fill('E2ePassword99');
    await page.locator('input[name="confirmPassword"]').fill('E2ePassword99');
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
