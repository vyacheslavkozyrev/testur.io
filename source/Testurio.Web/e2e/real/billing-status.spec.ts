/**
 * Billing Status specs — US-012
 *
 * AC-056–AC-058
 *
 * Validates the subscription section in Account Settings shows correct
 * plan details based on the current subscription state.
 */

import { test, expect } from '@playwright/test';

test.describe('Billing — Subscription Status Display', () => {
  test('subscription section visible; active/trialing subscription shows plan details and Manage billing button (AC-056, AC-057)', async ({ page, request }) => {
    // Check current subscription status
    const subRes = await request.get('/v1/billing/subscription');
    const sub = subRes.ok()
      ? (await subRes.json() as { status: string })
      : { status: 'None' };

    await page.goto('/settings', { waitUntil: 'networkidle' });

    // Navigate to the billing tab / subscription section if it is a separate tab
    const billingTab = page.getByRole('tab', { name: /billing|subscription/i });
    if (await billingTab.isVisible()) {
      await billingTab.click();
    }

    if (sub.status === 'Active' || sub.status === 'Trialing' || sub.status === 'CancelledPendingExpiry') {
      // AC-056: plan name, billing interval, current period end date visible
      await expect(
        page
          .locator('[data-testid="subscription-section"]')
          .or(page.getByText(/subscription|your plan/i).first()),
      ).toBeVisible({ timeout: 10_000 });

      await expect(
        page.locator('[data-testid="plan-name"]').or(page.getByText(/test pro|test junior|team|centurio/i).first()),
      ).toBeVisible();

      await expect(
        page
          .locator('[data-testid="billing-interval"]')
          .or(page.getByText(/monthly|annually|annual/i).first()),
      ).toBeVisible();

      await expect(
        page
          .locator('[data-testid="period-end-date"]')
          .or(page.getByText(/renews|expires|current period/i).first()),
      ).toBeVisible();

      // AC-057: "Manage billing" button visible
      await expect(
        page.getByRole('button', { name: /manage billing/i }).or(page.getByRole('link', { name: /manage billing/i })),
      ).toBeVisible();
    } else {
      // AC-058: no active subscription — CTA links to /pricing
      await expect(
        page
          .locator('[data-testid="no-subscription-cta"]')
          .or(page.getByRole('link', { name: /pricing|get started|start free trial/i }).first()),
      ).toBeVisible({ timeout: 10_000 });

      const ctaLink = page
        .getByRole('link', { name: /pricing|get started|start free trial/i })
        .first();

      await expect(ctaLink).toBeVisible();
      const href = await ctaLink.getAttribute('href');
      expect(href).toContain('/pricing');
    }
  });
});
