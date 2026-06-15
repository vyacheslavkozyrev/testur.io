/**
 * Forgot Password specs — US-009
 *
 * AC-038–AC-044
 *
 * The test navigates to /forgot-password and verifies the page structure and
 * basic interactions. It does NOT complete the full B2C password-reset flow
 * (that would require reading a real verification code from a mailbox).
 *
 * Implementation notes:
 * - The submit button label is "Send code" (t('forgotPassword.submitButton')).
 * - After a successful email submission B2C sends a verification code and the
 *   UI transitions to a code-entry step showing
 *   "We've sent a verification code to your email."
 *   (t('forgotPassword.codeSent')).
 * - "Back to sign in" is a link on the initial email form (not a post-submit
 *   confirmation screen).
 * - AC-042 (same response for unregistered email) cannot be verified E2E
 *   without inbox access — it is skipped with an explanation.
 */

import { test, expect, Browser } from '@playwright/test';

// Helper: open a fresh (unauthenticated) page with CF headers injected.
// storageState: undefined ensures no cookies from the project-level auth
// storage state bleed into these unauthenticated contexts.
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

test.describe('Forgot Password', () => {
  test('"Forgot password?" link on sign-in navigates to /forgot-password (AC-038)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/sign-in', { waitUntil: 'load' });

    await page.getByRole('link', { name: /forgot password/i }).click();

    await expect(page).toHaveURL(/\/forgot-password/, { timeout: 10_000 });

    await context.close();
  });

  test('forgot-password page renders Email field and submit button, not in shell layout (AC-039, AC-043)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'load' });

    // AC-039: Email field and submit button present.
    // The submit button label is "Send code" (auth.json → forgotPassword.submitButton).
    await expect(page.locator('input[name="email"], input[type="email"]').first()).toBeVisible();
    await expect(page.getByRole('button', { name: /send code/i })).toBeVisible();

    // AC-043: page is not in the shell layout (no sidebar)
    await expect(page.locator('[data-testid="sidebar"]')).not.toBeVisible();

    await context.close();
  });

  test('"Back to sign in" link on the email form navigates to /sign-in (AC-041)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'load' });

    // AC-041: "Back to sign in" link is present on the initial email form
    // and navigates back to /sign-in.
    await page.getByRole('link', { name: /back to sign in/i }).click();
    await expect(page).toHaveURL(/\/sign-in/, { timeout: 10_000 });

    await context.close();
  });

  test('submitting a valid email transitions to code-entry step (AC-040)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'load' });

    await page.locator('input[name="email"], input[type="email"]').first().fill(process.env.TEST_USER_EMAIL!);
    await page.getByRole('button', { name: /send code/i }).click();

    // AC-040: after submission B2C sends a verification code and the UI
    // transitions to the code-entry step with confirmation text.
    // Translation key: forgotPassword.codeSent =
    //   "We've sent a verification code to your email."
    await expect(
      page.getByText(/we'?ve sent a verification code to your email/i),
    ).toBeVisible({ timeout: 15_000 });

    await context.close();
  });

  test.skip('same response shown for unregistered email (AC-042)', async () => {
    // SKIPPED — AC-042 requires verifying that the UI shows the same
    // "code sent" step for an email address that has no B2C account.
    // B2C native-auth APIs may return an error for unknown accounts, meaning
    // the UI might show a generic error rather than the code-entry step.
    // Verifying the exact non-enumeration behaviour requires either:
    //   a) A guaranteed-unregistered test email that is still accepted by B2C, or
    //   b) Mocking the B2C API response.
    // Neither is available in the live E2E suite without inbox / admin access.
  });

  test('submitting with empty email shows validation error and form is not submitted (AC-044)', async ({ browser }: { browser: Browser }) => {
    const { context, page } = await openUnauthPage(browser);

    await page.goto('/forgot-password', { waitUntil: 'load' });

    // Click submit without filling the email field
    await page.getByRole('button', { name: /send code/i }).click();

    // AC-044: inline validation error appears (react-hook-form required rule).
    // Translation key: forgotPassword.emailRequired = "Email is required"
    await expect(
      page.getByText(/email is required|please enter your email|enter a valid email/i),
    ).toBeVisible({ timeout: 5_000 });

    // The code-entry step must NOT appear
    await expect(
      page.getByText(/we'?ve sent a verification code to your email/i),
    ).not.toBeVisible();

    await context.close();
  });
});
