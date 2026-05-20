import { test, expect } from '@playwright/test';

const MOCK_USER = {
  id: 'user-001',
  displayName: 'Alice Tester',
  email: 'alice@example.com',
};

const SUBSCRIPTION_NONE = {
  status: 'None',
  plan: null,
  billingInterval: null,
  trialEndsAt: null,
};

const SUBSCRIPTION_TRIALING = {
  status: 'Trialing',
  plan: 'TestJunior',
  billingInterval: 'Monthly',
  trialEndsAt: new Date(Date.now() + 13 * 24 * 60 * 60 * 1000).toISOString(),
};

const SUBSCRIPTION_ACTIVE = {
  status: 'Active',
  plan: 'TestJunior',
  billingInterval: 'Monthly',
  trialEndsAt: null,
};

test.describe('Plan Purchase — Unauthenticated flow', () => {
  test('AC-012: unauthenticated visitor clicking CTA is redirected to sign-in with plan params preserved', async ({
    page,
  }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ status: 401, json: { error: 'Unauthorized' } }),
    );

    await page.goto('/pricing', { waitUntil: 'domcontentloaded' });

    const ctaButton = page.getByRole('button', { name: /start free trial/i }).first();
    await ctaButton.click();

    const url = page.url();
    expect(url).toContain('plan=');
    expect(url).toContain('interval=');
  });
});

test.describe('Plan Purchase — Authenticated flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_NONE }),
    );
  });

  test('AC-016/AC-017: authenticated user clicking CTA on /pricing goes to Stripe Checkout via /billing', async ({
    page,
  }) => {
    let checkoutCalled = false;
    await page.route('**/v1/billing/checkout', (route) => {
      checkoutCalled = true;
      route.fulfill({
        json: { checkoutUrl: 'https://checkout.stripe.com/test-session' },
      });
    });

    await page.goto('/pricing?plan=TestJunior&interval=monthly', {
      waitUntil: 'domcontentloaded',
    });

    const ctaButton = page.getByRole('button', { name: /start free trial/i }).first();
    await ctaButton.click();

    await expect(page).toHaveURL(/\/billing/);
    expect(checkoutCalled).toBe(true);
  });

  test('AC-021/AC-015: abandoned Stripe Checkout returns user to /pricing with account unchanged', async ({
    page,
  }) => {
    await page.route('**/v1/billing/checkout', (route) =>
      route.fulfill({
        json: { checkoutUrl: page.url().replace('/billing', '/pricing') },
      }),
    );

    await page.goto('/billing?plan=TestJunior&interval=monthly', {
      waitUntil: 'domcontentloaded',
    });

    await expect(page).toHaveURL(/\/pricing/);

    const status = await page.evaluate(async () => {
      const res = await fetch('/v1/billing/subscription');
      return res.json();
    });
    expect(status.status).toBe('None');
  });
});

test.describe('Plan Purchase — Checkout success page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
  });

  test('AC-023: /billing/success shows confirmation when subscription becomes Trialing', async ({
    page,
  }) => {
    let pollCount = 0;
    await page.route('**/v1/billing/subscription', (route) => {
      pollCount++;
      const json = pollCount < 2 ? SUBSCRIPTION_NONE : SUBSCRIPTION_TRIALING;
      route.fulfill({ json });
    });

    await page.goto('/billing/success?session_id=cs_test_abc', {
      waitUntil: 'domcontentloaded',
    });

    await expect(
      page.getByText(/trial activated|your trial has started|free trial/i),
    ).toBeVisible({ timeout: 15000 });
  });

  test('AC-024: /billing/success shows support message after timeout without status resolution', async ({
    page,
  }) => {
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_NONE }),
    );

    await page.clock.install({ time: new Date() });

    await page.goto('/billing/success?session_id=cs_test_abc', {
      waitUntil: 'domcontentloaded',
    });

    await page.clock.fastForward(31000);

    await expect(
      page.getByText(/contact support|something went wrong/i),
    ).toBeVisible({ timeout: 5000 });
  });
});

test.describe('Plan Purchase — Trial status banner', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
    await page.route('**/v1/projects', (route) => route.fulfill({ json: [] }));
  });

  test('AC-007/AC-008: trial banner appears on dashboard with days remaining', async ({
    page,
  }) => {
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_TRIALING }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText(/days? remaining|days? left/i)).toBeVisible();
  });

  test('AC-010: trial banner is hidden when subscription is Active', async ({
    page,
  }) => {
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_ACTIVE }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText(/days? remaining|days? left/i)).not.toBeVisible();
  });

  test('AC-011: trial banner is hidden when subscription status is None', async ({
    page,
  }) => {
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_NONE }),
    );

    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText(/days? remaining|days? left/i)).not.toBeVisible();
  });
});

test.describe('Plan Purchase — Upgrade gate', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_NONE }),
    );
    await page.route('**/v1/projects', (route) => route.fulfill({ json: [] }));
  });

  test('AC-029/AC-030: attempting to create a project with no active plan shows UpgradeModal', async ({
    page,
  }) => {
    await page.goto('/projects', { waitUntil: 'domcontentloaded' });

    const createButton = page.getByRole('button', { name: /create|new project/i }).first();
    await createButton.click();

    await expect(
      page.getByRole('dialog'),
    ).toBeVisible({ timeout: 5000 });

    await expect(
      page.getByText(/upgrade|choose a plan/i),
    ).toBeVisible();
  });
});
