import { test, expect } from '@playwright/test';

const MOCK_PLANS = [
  {
    id: 'test-junior',
    name: 'Test Junior',
    monthlyPrice: 0,
    annualPrice: 0,
    annualDiscountPercent: 0,
    isPopular: false,
    displayFeatures: ['Up to 3 projects', '50 test runs / month'],
    limits: { maxProjects: 3, maxTestRunsPerMonth: 50 },
    features: { apiTesting: true, uiE2eTesting: false, aiMemory: false, pmReportPostBack: true },
  },
  {
    id: 'test-pro',
    name: 'Test Pro',
    monthlyPrice: 49,
    annualPrice: 470,
    annualDiscountPercent: 20,
    isPopular: true,
    displayFeatures: ['Up to 10 projects', 'Unlimited test runs'],
    limits: { maxProjects: 10, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
];

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
  test('AC-012: unauthenticated visitor CTA links to /sign-up with plan params preserved', async ({
    page,
  }) => {
    // Clear cookies so the project-level storageState session doesn't bleed in.
    await page.context().clearCookies();

    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ status: 401, body: '' }),
    );
    await page.route('**/v1/plans', (route) =>
      route.fulfill({ json: MOCK_PLANS }),
    );

    await page.goto('/pricing', { waitUntil: 'networkidle' });

    const ctaLink = page.getByRole('link', { name: /get started free/i }).first();
    await expect(ctaLink).toBeVisible({ timeout: 5_000 });

    const href = await ctaLink.getAttribute('href');
    expect(href).toMatch(/\/sign-up/);
    expect(href).toContain('plan=');
    expect(href).toContain('interval=');
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
    await page.route('**/v1/plans', (route) =>
      route.fulfill({ json: MOCK_PLANS }),
    );
  });

  test('AC-016/AC-017: authenticated user clicking CTA on /pricing goes to Stripe Checkout via /billing', async ({
    page,
  }) => {
    let checkoutCalled = false;
    await page.route('**/v1/billing/checkout', (route) => {
      checkoutCalled = true;
      route.fulfill({
        json: { checkoutUrl: '/pricing?checkout=done' },
      });
    });

    await page.goto('/pricing?plan=TestJunior&interval=monthly', {
      waitUntil: 'domcontentloaded',
    });

    // Authenticated users see "Upgrade" CTA (a link to /billing?plan=...).
    const ctaLink = page.getByRole('link', { name: /upgrade/i }).first();
    await ctaLink.click();

    await expect(page).toHaveURL(/checkout=done/);
    expect(checkoutCalled).toBe(true);
  });

  test('AC-021/AC-015: abandoned Stripe Checkout returns user to /pricing with account unchanged', async ({
    page,
  }) => {
    await page.route('**/v1/billing/checkout', (route) =>
      route.fulfill({
        json: { checkoutUrl: '/pricing?abandoned=1' },
      }),
    );

    const checkoutDone = page.waitForResponse((resp) =>
      resp.url().includes('/v1/billing/checkout'),
    );

    await page.goto('/billing?plan=TestJunior&interval=monthly', {
      waitUntil: 'domcontentloaded',
    });

    await checkoutDone;

    await expect(page).toHaveURL(/\/pricing/, { timeout: 10_000 });

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
      page.getByText(/trial activated|your trial has started|free trial/i).first(),
    ).toBeVisible({ timeout: 15000 });
  });

  test('AC-024: /billing/success shows support message after timeout without status resolution', async ({
    page,
  }) => {
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({ json: SUBSCRIPTION_NONE }),
    );
    await page.route('**/v1/billing/sync-session', (route) =>
      route.fulfill({ json: SUBSCRIPTION_NONE }),
    );

    await page.clock.install({ time: new Date() });

    // sync-session is called inside the same useEffect that registers the 30s setTimeout.
    // Waiting for its response guarantees the timer is registered before we advance the clock.
    const syncDone = page.waitForResponse((resp) =>
      resp.url().includes('/v1/billing/sync-session'),
    );

    await page.goto('/billing/success?session_id=cs_test_abc', {
      waitUntil: 'domcontentloaded',
    });

    await syncDone;

    await page.clock.fastForward(31000);

    await expect(
      page.getByText(/contact support/i),
    ).toBeVisible({ timeout: 10_000 });
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
    await page.waitForResponse((resp) => resp.url().includes('/v1/billing/subscription'));

    const createButton = page.getByRole('button', { name: /create|new project/i }).first();
    await createButton.click();

    await expect(
      page.getByRole('dialog'),
    ).toBeVisible({ timeout: 5000 });

    await expect(
      page.getByText(/subscription required/i),
    ).toBeVisible();
  });
});
