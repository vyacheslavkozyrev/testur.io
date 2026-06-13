import { test, expect } from '@playwright/test';

test.describe('Subscription', () => {
  test('purchases Test Pro plan via Stripe Checkout', async ({ page, request }) => {
    test.setTimeout(120_000);
    // Idempotent — skip if the user already has an active or trialing subscription
    const subCheck = await request.get('/v1/billing/subscription');
    if (subCheck.ok()) {
      const sub = await subCheck.json() as { status: string };
      if (sub.status === 'Active' || sub.status === 'Trialing') {
        test.skip(true, `Subscription already ${sub.status} — skipping purchase`);
        return;
      }
    }

    // ── 1. Pricing page ───────────────────────────────────────────────────────
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Find the Test Pro card by plan name heading and click its Upgrade button
    const testProCard = page.locator('h5', { hasText: 'Test Pro' }).locator('../..');
    await testProCard.getByRole('link', { name: 'Upgrade' }).click();

    // ── 2. Stripe Checkout ────────────────────────────────────────────────────
    await page.waitForURL(/checkout\.stripe\.com/, { timeout: 15_000 });

    // Email — Stripe may pre-fill it; only type if the field is empty
    const emailInput = page.locator('input[type="email"]').first();
    if (await emailInput.isVisible()) {
      const currentValue = await emailInput.inputValue();
      if (!currentValue) await emailInput.fill(process.env.TEST_USER_EMAIL!);
    }

    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000); // let Stripe's JS fully render the payment form

    // Uncheck "Save my information for faster checkout" to dismiss the Stripe Link overlay.
    // When Link is active, the card accordion is hidden; unchecking reveals the payment method selector.
    const saveInfoCheckbox = page.getByLabel('Save my information for faster checkout');
    if (await saveInfoCheckbox.isVisible() && await saveInfoCheckbox.isChecked()) {
      await saveInfoCheckbox.uncheck();
      await page.waitForTimeout(1000);
    }

    // The card accordion button gets a large pointer-events overlay after Link dismissal.
    // JS click bypasses Playwright's visibility/pointer-events checks.
    await page.evaluate(() => {
      (document.querySelector('[data-testid="card-accordion-item-button"]') as HTMLElement | null)?.click();
    });
    await page.waitForTimeout(2000);

    // Card fields are rendered in the Stripe Checkout page DOM (same origin — no cross-origin iframe).
    await page.locator('[placeholder="1234 1234 1234 1234"]').fill('4242 4242 4242 4242');
    await page.locator('[placeholder="MM / YY"]').fill('12 / 28');
    await page.locator('[placeholder="CVC"]').fill('123');

    const nameInput = page.locator('input[placeholder="Full name on card"], input[placeholder="Name on card"], input[autocomplete="cc-name"]');
    if (await nameInput.isVisible()) await nameInput.fill('E2E Test');

    const zipInput = page.locator('input[placeholder="ZIP"]');
    if (await zipInput.isVisible()) await zipInput.fill('10001');

    // Submit
    await page.getByRole('button', { name: 'Start trial' }).click();

    // ── 3. Success page ───────────────────────────────────────────────────────
    await page.waitForURL('**/billing/success**', { timeout: 60_000 });

    // The page polls until the subscription is confirmed (up to 30 s)
    await expect(
      page.getByRole('heading', { name: 'Your free trial has started!' }),
    ).toBeVisible({ timeout: 35_000 });

    await expect(
      page.getByRole('link', { name: 'Create your first project' }),
    ).toBeVisible();
  });
});
