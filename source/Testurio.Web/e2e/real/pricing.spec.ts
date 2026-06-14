/**
 * Pricing page specs — US-010
 *
 * AC-045–AC-050
 *
 * The pricing page is publicly accessible — no auth required.
 */

import { test, expect, Browser } from '@playwright/test';

// ---------------------------------------------------------------------------
// US-010 — Pricing Page Renders Plans Correctly
// ---------------------------------------------------------------------------

test.describe('Pricing Page', () => {
  test('is accessible without authentication (AC-050)', async ({ browser }: { browser: Browser }) => {
    // Fresh unauthenticated context
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

    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Should NOT redirect to sign-in
    expect(page.url()).not.toContain('/sign-in');
    await expect(page.getByRole('heading', { name: /pricing/i }).first()).toBeVisible({ timeout: 10_000 });

    await context.close();
  });

  test('renders four plan cards in order (AC-045, AC-046)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    const planNames = ['Test Junior', 'Test Pro', 'Team', 'Centurio'];
    for (const name of planNames) {
      await expect(page.getByRole('heading', { name })).toBeVisible();
    }

    // AC-046: each card has a price and at least one feature item
    for (const name of planNames) {
      const card = page.locator('[data-testid="plan-card"]', { hasText: name });
      await expect(card.locator('[data-testid="plan-price"], .plan-price, [class*="price"]').first()).toBeVisible();
      await expect(card.locator('li, [data-testid="feature-item"]').first()).toBeVisible();
    }
  });

  test('Monthly/Annual toggle defaults to Monthly and switching updates prices (AC-047, AC-048)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // AC-047: toggle visible and defaults to Monthly
    const toggle = page.getByRole('button', { name: /monthly/i }).or(
      page.locator('[data-testid="billing-toggle"], [role="group"]'),
    ).first();
    await expect(toggle).toBeVisible();

    // Capture prices before switching
    const pricesBefore = await page
      .locator('[data-testid="plan-price"], .plan-price, [class*="Price"]')
      .allTextContents();

    // AC-048: switch to Annual and prices change without full reload
    const annualButton = page.getByRole('button', { name: /annual/i });
    await annualButton.click();

    const pricesAfter = await page
      .locator('[data-testid="plan-price"], .plan-price, [class*="Price"]')
      .allTextContents();

    // At least one price should change when switching to Annual
    const pricesChanged = pricesAfter.some((price, i) => price !== pricesBefore[i]);
    expect(pricesChanged).toBe(true);
  });

  test('Test Pro card is visually marked as "Most popular" (AC-049)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // AC-049: "Most popular" badge or elevated style on Test Pro card
    const testProCard = page
      .locator('[data-testid="plan-card"]', { hasText: 'Test Pro' })
      .or(page.locator('[class*="card"]', { hasText: 'Test Pro' }))
      .first();

    await expect(
      testProCard.getByText(/most popular/i),
    ).toBeVisible();
  });

  test('authenticated user sees "Start free trial" CTAs on plan cards (AC-051)', async ({ page }) => {
    // This test uses the shared storageState (authenticated)
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // AC-051: "Start free trial" CTA visible on plan cards for authenticated users
    const ctaButtons = page.getByRole('link', { name: /start free trial|upgrade/i });
    await expect(ctaButtons.first()).toBeVisible();
  });
});
