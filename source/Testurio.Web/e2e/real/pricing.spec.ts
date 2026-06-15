/**
 * Pricing page specs — US-010
 *
 * AC-045–AC-050
 *
 * The pricing page is publicly accessible — no auth required.
 *
 * Implementation notes:
 * - Plan cards are rendered by <PlanCard> component; the root Box has no
 *   data-testid. Cards are located by their h5 plan-name heading.
 * - Price text is rendered as Typography with "$XX" or "Free"; no data-testid.
 *   The toggle test captures all price-like text and checks for change.
 * - The billing toggle is a MUI ToggleButtonGroup; each option renders as a
 *   <button> with aria-label "Monthly" / "Annual".
 * - "Most popular" badge is a MUI Chip labelled t('planCard.mostPopular') =
 *   "Most popular", shown only on the Test Pro card (isPopular: true).
 */

import { test, expect, Browser } from '@playwright/test';

test.describe('Pricing Page', () => {
  test('is accessible without authentication (AC-050)', async ({ browser }: { browser: Browser }) => {
    // Fresh unauthenticated context — storageState: undefined prevents the
    // project-level auth state from being applied.
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

    await page.goto('/pricing', { waitUntil: 'load' });

    // Should NOT redirect to sign-in
    expect(page.url()).not.toContain('/sign-in');
    // Page title heading: t('page.title') = "Choose the plan that's right for you"
    await expect(page.getByRole('heading', { name: /choose the plan|pricing/i }).first()).toBeVisible({ timeout: 10_000 });

    await context.close();
  });

  test('renders four plan cards in order (AC-045, AC-046)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'load' });

    const planNames = ['Test Junior', 'Test Pro', 'Team', 'Centurio'];

    // AC-045: all four plan name headings are visible (rendered as <h5> by PlanCard)
    for (const name of planNames) {
      await expect(page.getByRole('heading', { name, exact: true })).toBeVisible({ timeout: 15_000 });
    }

    // AC-046: each card section has at least one feature list item.
    // PlanCard renders features as <li> elements inside an MUI List inside
    // styles.inner Box. The heading <h5> is a direct child of styles.inner.
    // DOM path: <h5> → styles.inner (Box) → contains the <ul>/<li> elements.
    for (const name of planNames) {
      const heading = page.getByRole('heading', { name, exact: true });
      await expect(heading).toBeVisible();

      // heading.locator('..') → styles.inner Box which also contains the List
      const cardInner = heading.locator('..');
      await expect(cardInner.locator('li').first()).toBeVisible();
    }
  });

  test('Monthly/Annual toggle defaults to Monthly and switching updates prices (AC-047, AC-048)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'load' });

    // Wait for plan cards to load
    await expect(page.getByRole('heading', { name: 'Test Pro', exact: true })).toBeVisible({ timeout: 15_000 });

    // AC-047: toggle visible — MUI ToggleButton renders as <button> with
    // aria-label from t('billingToggle.monthly') = "Monthly"
    const monthlyButton = page.getByRole('button', { name: 'Monthly', exact: true });
    await expect(monthlyButton).toBeVisible();

    // Capture all dollar-amount typography text before switching.
    // PlanCard renders prices as Typography containing "$XX" or "Free".
    const priceLocator = page.getByText(/^\$\d+$|^Free$/);
    const pricesBefore = await priceLocator.allTextContents();

    // AC-048: switch to Annual — MUI ToggleButton with aria-label "Annual"
    const annualButton = page.getByRole('button', { name: 'Annual', exact: true });
    await annualButton.click();

    // Wait for at least one price to update
    const pricesAfter = await priceLocator.allTextContents();

    // At least one price should change when switching to Annual (annual ÷ 12)
    const pricesChanged = pricesAfter.some((price, i) => price !== pricesBefore[i]);
    expect(pricesChanged).toBe(true);
  });

  test('Test Pro card is visually marked as "Most popular" (AC-049)', async ({ page }) => {
    await page.goto('/pricing', { waitUntil: 'load' });

    // Wait for plan cards to load
    await expect(page.getByRole('heading', { name: 'Test Pro', exact: true })).toBeVisible({ timeout: 15_000 });

    // AC-049: "Most popular" badge is shown on the Test Pro card.
    // PlanCard renders an MUI Chip with label t('planCard.mostPopular') = "Most popular"
    // only when plan.isPopular is true (Test Pro is the only popular plan).
    //
    // DOM structure in PlanCard:
    //   <Box sx={styles.root}>          ← card root (position: relative)
    //     <Box sx={styles.popularBadgeWrapper}>  ← badge (position: absolute)
    //       <Chip label="Most popular" />
    //     </Box>
    //     <Box sx={styles.inner}>        ← inner content
    //       <Typography variant="h5">Test Pro</Typography>   ← heading
    //       ...
    //     </Box>
    //   </Box>
    //
    // heading.locator('..') → styles.inner
    // heading.locator('../..') → styles.root (contains the badge sibling)
    const testProHeading = page.getByRole('heading', { name: 'Test Pro', exact: true });
    const testProCardRoot = testProHeading.locator('../..');

    await expect(
      testProCardRoot.getByText(/most popular/i),
    ).toBeVisible();
  });

  test('authenticated user sees CTA buttons on plan cards (AC-051)', async ({ page }) => {
    // This test uses the shared storageState (authenticated)
    await page.goto('/pricing', { waitUntil: 'load' });

    // Wait for plan cards to load
    await expect(page.getByRole('heading', { name: 'Test Pro', exact: true })).toBeVisible({ timeout: 15_000 });

    // AC-051: CTA buttons visible on plan cards for authenticated users.
    // For authenticated users PlanCard shows "Upgrade" or "Current plan" or "Get started free".
    const ctaButtons = page.getByRole('link', { name: /get started free|upgrade|current plan/i });
    await expect(ctaButtons.first()).toBeVisible();
  });
});
