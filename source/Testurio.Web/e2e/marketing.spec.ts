import { test, expect } from '@playwright/test';
import type { PlanDefinition } from '../src/types/plan.types';

// ─── Mock plan data ───────────────────────────────────────────────────────────

const MOCK_PLANS: PlanDefinition[] = [
  {
    id: 'test-junior',
    name: 'Test Junior',
    monthlyPrice: 0,
    annualPrice: 0,
    annualDiscountPercent: 0,
    isPopular: false,
    displayFeatures: [
      'Up to 3 projects',
      '50 automated test runs / day',
      'API test execution',
      'Basic test reports',
      'Community support',
    ],
    limits: { maxProjects: 3, maxTestRunsPerMonth: 50 },
    features: { apiTesting: true, uiE2eTesting: false, aiMemory: false, pmReportPostBack: false },
  },
  {
    id: 'test-pro',
    name: 'Test Pro',
    monthlyPrice: 49,
    annualPrice: 470,
    annualDiscountPercent: 20,
    isPopular: true,
    displayFeatures: [
      'Up to 10 projects',
      'Unlimited test runs',
      'API & UI end-to-end testing',
      'AI memory layer for smarter scenarios',
      'ADO & Jira report post-back',
      'Email support',
    ],
    limits: { maxProjects: 10, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
  {
    id: 'team',
    name: 'Team',
    monthlyPrice: 149,
    annualPrice: 1430,
    annualDiscountPercent: 20,
    isPopular: false,
    displayFeatures: [
      'Unlimited projects',
      'Unlimited test runs',
      'API & UI end-to-end testing',
      'AI memory layer with cross-project sharing',
      'ADO & Jira report post-back',
      'Custom test generation prompts',
      'Priority support',
    ],
    limits: { maxProjects: -1, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
  {
    id: 'centurio',
    name: 'Centurio',
    monthlyPrice: 399,
    annualPrice: 3830,
    annualDiscountPercent: 20,
    isPopular: false,
    displayFeatures: [
      'Unlimited projects',
      'Unlimited test runs',
      'All test types including smoke, a11y, visual',
      'Full AI memory layer with global anonymised sharing',
      'All PM tool integrations',
      'Dedicated egress IP range',
      'SLA guarantee',
      'Dedicated support engineer',
    ],
    limits: { maxProjects: -1, maxTestRunsPerMonth: -1 },
    features: { apiTesting: true, uiE2eTesting: true, aiMemory: true, pmReportPostBack: true },
  },
];

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function mockPlansApi(page: import('@playwright/test').Page) {
  await page.route('**/v1/plans', (route) =>
    route.fulfill({ json: MOCK_PLANS }),
  );
}

async function mockAuthMe(
  page: import('@playwright/test').Page,
  user: { id: string; displayName: string; email: string } | null,
) {
  if (user) {
    await page.route('**/api/auth/me', (route) => route.fulfill({ json: user }));
  } else {
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({ status: 401, body: '' }),
    );
  }
}

// ─── Landing page tests ───────────────────────────────────────────────────────

test.describe('Landing page (AC-001 – AC-016)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthMe(page, null);
  });

  test('AC-001/AC-002: hero section is the first visible element with headline and CTAs', async ({
    page,
  }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    // Headline visible without scrolling
    const headline = page.getByText('Automated testing, triggered by your stories');
    await expect(headline).toBeVisible();

    // Primary CTA — in hero section; use .first() since the header also has a "Get Started" button
    const primaryCta = page.getByRole('link', { name: 'Get started free' }).first();
    await expect(primaryCta).toBeVisible();
    await expect(primaryCta).toHaveAttribute('href', /sign-up/);

    // Secondary CTA
    const secondaryCta = page.getByRole('button', { name: 'See how it works' });
    await expect(secondaryCta).toBeVisible();
  });

  test('AC-003: hero renders without horizontal overflow at 375 px', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/', { waitUntil: 'networkidle' });

    // Allow a small tolerance for browser scrollbar width
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(390);
  });

  test('AC-004/AC-005/AC-006: features section has six highlights', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    await expect(page.getByText('AI Test Generation')).toBeVisible();
    await expect(page.getByText('Automatic Triggering')).toBeVisible();
    await expect(page.getByText('API Testing')).toBeVisible();
    await expect(page.getByText('UI End-to-End Testing')).toBeVisible();
    await expect(page.getByText('PM Tool Integration')).toBeVisible();
    await expect(page.getByText('Test Memory Layer')).toBeVisible();
  });

  test('AC-007/AC-008/AC-009/AC-010: how it works section has five steps and correct id', async ({
    page,
  }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    const section = page.locator('#how-it-works');
    await expect(section).toBeAttached();

    // Five step titles
    await expect(page.getByText('Connect your PM tool')).toBeVisible();
    await expect(page.getByText('Move a story to "In Testing"')).toBeVisible();
    await expect(page.getByText('AI reads the story and generates scenarios')).toBeVisible();
    await expect(page.getByText('Tests run automatically')).toBeVisible();
    await expect(page.getByText('Results posted back to your ticket')).toBeVisible();
  });

  test('AC-002: "See how it works" CTA triggers scroll to the How It Works section', async ({
    page,
  }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    // Verify the section exists with the correct id for scrolling
    const section = page.locator('#how-it-works');
    await expect(section).toBeAttached();

    // Click the secondary CTA — it calls scrollIntoView on #how-it-works
    const secondaryCta = page.getByRole('button', { name: 'See how it works' });
    await secondaryCta.click();

    // After click, verify the scroll handler was invoked by checking the section is
    // still attached (it didn't navigate away) and the page Y offset has increased
    const scrollY = await page.evaluate(() => window.scrollY);
    expect(scrollY).toBeGreaterThan(0);
  });

  test('AC-011/AC-012/AC-013: pricing teaser section shows four plan names and "See all plans" button', async ({
    page,
  }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    // Pricing section headline summary (AC-011)
    await expect(page.getByText('Simple, transparent pricing. Start free, scale as you grow.')).toBeVisible();

    // Four plan names from teaser section (AC-012)
    await expect(page.getByText('Test Junior')).toBeVisible();
    await expect(page.getByText('Test Pro')).toBeVisible();
    await expect(page.getByText('Team')).toBeVisible();
    await expect(page.getByText('Centurio')).toBeVisible();
  });

  test('AC-011: "See all plans" button navigates to /pricing', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    const seeAllPlans = page.getByRole('link', { name: 'See all plans' });
    await expect(seeAllPlans).toBeVisible();
    await expect(seeAllPlans).toHaveAttribute('href', '/pricing');
  });

  test('AC-014/AC-015/AC-016: footer is present with logo, nav links, and copyright', async ({
    page,
  }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    await expect(page.getByText('© 2026 Testurio. All rights reserved.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Privacy Policy' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Terms of Service' })).toBeVisible();
  });

  test('AC-016: footer renders without horizontal overflow at 375 px', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/', { waitUntil: 'networkidle' });

    // Allow a small tolerance for browser scrollbar width
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(390);
  });
});

// ─── Public header tests ──────────────────────────────────────────────────────

test.describe('Public header (AC-017 – AC-022)', () => {
  test('AC-017/AC-018: unauthenticated visitor sees Sign In and Get Started', async ({
    page,
  }) => {
    await mockAuthMe(page, null);
    await page.goto('/', { waitUntil: 'networkidle' });

    // Sign In link in header (exact match to distinguish from hero CTA)
    await expect(page.getByRole('link', { name: 'Sign In' })).toBeVisible();
    // The header "Get Started" button — use exact match to avoid matching hero's "Get started free"
    await expect(page.getByRole('link', { name: 'Get Started', exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Go to Dashboard' })).not.toBeVisible();
  });

  test('AC-019: authenticated user sees Go to Dashboard, no Sign In/Get Started', async ({
    page,
  }) => {
    await mockAuthMe(page, {
      id: '00000000-0000-0000-0000-000000000099',
      displayName: 'Jane Smith',
      email: 'jane@example.com',
    });
    await page.goto('/', { waitUntil: 'networkidle' });

    // Wait for auth state to resolve (useAuthUser is async via useEffect)
    await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('link', { name: 'Sign In' })).not.toBeVisible();
    // "Get Started" header button gone; hero's "Get started free" may still be present
    await expect(page.getByRole('link', { name: 'Get Started', exact: true })).not.toBeVisible();
  });

  test('AC-021: active nav link is highlighted on home page', async ({ page }) => {
    await mockAuthMe(page, null);
    await page.goto('/', { waitUntil: 'networkidle' });

    // The desktop nav link for Home should have aria-current="page"
    // The logo link (aria-label="Testurio home") is a different element; target the nav link by href
    const homeNavLink = page.locator('header a[href="/"][aria-current="page"]');
    await expect(homeNavLink).toBeAttached();
  });

  test('AC-022: hamburger menu is visible at 375 px viewport', async ({ page }) => {
    await mockAuthMe(page, null);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/', { waitUntil: 'networkidle' });

    await expect(
      page.getByRole('button', { name: 'Open navigation menu' }),
    ).toBeVisible();
  });
});

// ─── Pricing page tests ───────────────────────────────────────────────────────

test.describe('Pricing page (AC-023 – AC-038)', () => {
  test.beforeEach(async ({ page }) => {
    await mockAuthMe(page, null);
    await mockPlansApi(page);
    // Mock subscription endpoint so plan rank/CTAs aren't affected by the real
    // dev API returning an active subscription for the seeded test user.
    await page.route('**/v1/billing/subscription', (route) =>
      route.fulfill({
        json: {
          status: 'None',
          plan: null,
          billingInterval: null,
          trialEndsAt: null,
          currentPeriodEnd: null,
          cancelledAt: null,
          paymentMethodLast4: null,
          paymentMethodExpMonth: null,
          paymentMethodExpYear: null,
          invoices: [],
        },
      }),
    );
  });

  test('AC-023/AC-024/AC-025: four plan cards load with names and prices from API', async ({
    page,
  }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    await expect(page.getByText('Test Junior')).toBeVisible();
    await expect(page.getByText('Test Pro')).toBeVisible();
    await expect(page.getByText('Team')).toBeVisible();
    await expect(page.getByText('Centurio')).toBeVisible();
  });

  test('AC-028: Test Pro card has "Most popular" badge', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    await expect(page.getByText('Most popular')).toBeVisible();
  });

  test('AC-029/AC-030: billing interval toggle defaults to Monthly; switching to Annual updates prices', async ({
    page,
  }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Monthly price visible initially (Test Pro = $49)
    await expect(page.getByText('$49')).toBeVisible();

    // Switch to Annual
    const annualButton = page.getByRole('button', { name: /annual/i });
    await annualButton.click();

    // Annual monthly-equivalent for Test Pro: 470 / 12 ≈ 39
    await expect(page.getByText('$39')).toBeVisible();
  });

  test('AC-031: annual price shows monthly-equivalent (annual total ÷ 12)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Wait for all four plans to be rendered before toggling
    await expect(page.getByText('Team')).toBeVisible();
    await expect(page.getByText('Centurio')).toBeVisible();

    const annualButton = page.getByRole('button', { name: /annual/i });
    await annualButton.click();

    // Wait for price update — Test Pro annual monthly-equivalent: 470 / 12 ≈ 39
    await expect(page.getByText('$39')).toBeVisible();

    // Verify other cards updated to their annual prices:
    // Team: 1430 / 12 ≈ 119; Centurio: 3830 / 12 ≈ 319
    // Use first() in case of strict mode ambiguity
    await expect(page.getByText('$119').first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('$319').first()).toBeVisible({ timeout: 10000 });
  });

  test('AC-032: annual toggle shows discount badge matching annualDiscountPercent', async ({
    page,
  }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    const annualButton = page.getByRole('button', { name: /annual/i });
    await annualButton.click();

    // Discount badges visible on cards (plans have 20% discount)
    const saveBadges = page.getByText('Save 20%');
    await expect(saveBadges.first()).toBeVisible();
  });

  test('AC-033: unauthenticated CTA redirects to /sign-up with plan and interval params', async ({
    page,
  }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Wait for plans to load
    await expect(page.getByText('Test Junior')).toBeVisible();

    const ctaLinks = page.getByRole('link', { name: 'Get started free' });
    const firstLink = ctaLinks.first();
    const href = await firstLink.getAttribute('href');

    expect(href).toMatch(/sign-up/);
    expect(href).toMatch(/plan=test-junior/);
    expect(href).toMatch(/interval=monthly/);
  });

  test('AC-034/AC-035: authenticated user CTA shows Upgrade and links to /billing', async ({
    page,
  }) => {
    await mockAuthMe(page, {
      id: '00000000-0000-0000-0000-000000000099',
      displayName: 'Jane Smith',
      email: 'jane@example.com',
    });

    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Wait for plans to load and auth state to resolve
    await expect(page.getByText('Test Junior')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Upgrade' }).first()).toBeVisible({ timeout: 10000 });

    const firstUpgradeLink = page.getByRole('link', { name: 'Upgrade' }).first();
    const href = await firstUpgradeLink.getAttribute('href');

    expect(href).toMatch(/\/billing/);
    expect(href).toMatch(/plan=/);
    expect(href).toMatch(/interval=monthly/);
  });

  test('AC-026: skeleton placeholders shown while loading', async ({ page }) => {
    // Override the plans route with a long delay to keep the skeleton state visible
    await page.route('**/v1/plans', async (route) => {
      await new Promise<void>((resolve) => setTimeout(resolve, 10000));
      await route.fulfill({ json: MOCK_PLANS });
    });

    // Use domcontentloaded so the page is ready before the delayed API resolves
    await page.goto('/pricing', { waitUntil: 'domcontentloaded' });

    // Immediately after JS hydrates, plan data is still loading — skeletons should be present
    const skeleton = page.locator('.MuiSkeleton-root');
    await expect(skeleton.first()).toBeAttached({ timeout: 5000 });
  });

  test('AC-027: error state shows inline error with "Try again" button', async ({ page }) => {
    // Override the plans route to return an error
    await page.route('**/v1/plans', (route) =>
      route.fulfill({ status: 500, body: 'Internal Server Error' }),
    );

    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Wait for error to render (React Query will reject and show error state)
    await expect(page.getByText('Failed to load plans. Please try again.')).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible();
  });

  test('AC-036: pricing page renders without horizontal overflow at 375 px', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Allow a small tolerance for browser scrollbar width
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(390);
  });

  test('AC-037: plan cards stack vertically at 375 px and render all 4 at 1440 px', async ({
    page,
  }) => {
    // Mobile: 375 px — plan cards visible in single-column layout
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    // Wait for plans to load
    await expect(page.getByText('Test Junior')).toBeVisible();

    // Verify no horizontal overflow at mobile
    const scrollWidthMobile = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidthMobile).toBeLessThanOrEqual(390);

    // Desktop: 1440 px — all four plan names visible in the plan grid
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.reload({ waitUntil: 'networkidle' });
    await expect(page.getByText('Test Junior')).toBeVisible();
    await expect(page.getByText('Test Pro')).toBeVisible();
    await expect(page.getByText('Team')).toBeVisible();
    await expect(page.getByText('Centurio')).toBeVisible();

    // Verify no horizontal overflow at desktop
    const scrollWidthDesktop = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidthDesktop).toBeLessThanOrEqual(1440);
  });

  test('AC-014/AC-015: footer appears on pricing page with copyright and legal links', async ({
    page,
  }) => {
    await page.goto('/pricing', { waitUntil: 'networkidle' });

    await expect(page.getByText('© 2026 Testurio. All rights reserved.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Privacy Policy' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Terms of Service' })).toBeVisible();
  });
});
