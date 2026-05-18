# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: marketing.spec.ts >> Pricing page (AC-023 – AC-038) >> AC-031: annual price shows monthly-equivalent (annual total ÷ 12)
- Location: e2e\marketing.spec.ts:301:7

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByText('$119')
Expected: visible
Timeout: 5000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" with timeout 5000ms
  - waiting for getByText('$119')

```

```yaml
- banner:
  - link "Testurio home":
    - /url: /
    - heading "Testurio" [level=6]
  - link "Home":
    - /url: /
  - link "Pricing":
    - /url: /pricing
  - link "Sign In":
    - /url: /sign-in
  - link "Get Started":
    - /url: /sign-up
- main:
  - heading "Choose the plan that's right for you" [level=2]
  - paragraph: Start free. Upgrade when you're ready. Cancel any time.
  - group "Billing interval":
    - button "Monthly" [pressed]:
      - paragraph: Monthly
    - button "Annual":
      - paragraph: Annual
      - text: Save 20%
  - heading "Test Junior" [level=5]
  - paragraph: Free
  - separator
  - list:
    - listitem: Up to 3 projects
    - listitem: 50 automated test runs / day
    - listitem: API test execution
    - listitem: Basic test reports
    - listitem: Community support
  - link "Get started free":
    - /url: /sign-up?plan=test-junior&interval=monthly
  - text: Most popular
  - heading "Test Pro" [level=5]
  - paragraph: $49
  - paragraph: / mo
  - separator
  - list:
    - listitem: Up to 10 projects
    - listitem: Unlimited test runs
    - listitem: API & UI end-to-end testing
    - listitem: AI memory layer for smarter scenarios
    - listitem: ADO & Jira report post-back
    - listitem: Email support
  - link "Get started free":
    - /url: /sign-up?plan=test-pro&interval=monthly
  - heading "Team" [level=5]
  - paragraph: $149
  - paragraph: / mo
  - separator
  - list:
    - listitem: Unlimited projects
    - listitem: Unlimited test runs
    - listitem: API & UI end-to-end testing
    - listitem: AI memory layer with cross-project sharing
    - listitem: ADO & Jira report post-back
    - listitem: Custom test generation prompts
    - listitem: Priority support
  - link "Get started free":
    - /url: /sign-up?plan=team&interval=monthly
  - heading "Centurio" [level=5]
  - paragraph: $399
  - paragraph: / mo
  - separator
  - list:
    - listitem: Unlimited projects
    - listitem: Unlimited test runs
    - listitem: All test types including smoke, a11y, visual
    - listitem: Full AI memory layer with global anonymised sharing
    - listitem: All PM tool integrations
    - listitem: Dedicated egress IP range
    - listitem: SLA guarantee
    - listitem: Dedicated support engineer
  - link "Get started free":
    - /url: /sign-up?plan=centurio&interval=monthly
- contentinfo:
  - separator
  - link "Testurio home":
    - /url: /
    - heading "Testurio" [level=6]
  - link "Home":
    - /url: /
  - link "Pricing":
    - /url: /pricing
  - link "Privacy Policy":
    - /url: "#"
  - link "Terms of Service":
    - /url: "#"
  - paragraph: © 2026 Testurio. All rights reserved.
- alert
- button "Open Next.js Dev Tools":
  - img
```

# Test source

```ts
  210 |   test('AC-016: footer renders without horizontal overflow at 375 px', async ({ page }) => {
  211 |     await page.setViewportSize({ width: 375, height: 812 });
  212 |     await page.goto('/', { waitUntil: 'networkidle' });
  213 | 
  214 |     // Allow a small tolerance for browser scrollbar width
  215 |     const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  216 |     expect(scrollWidth).toBeLessThanOrEqual(390);
  217 |   });
  218 | });
  219 | 
  220 | // ─── Public header tests ──────────────────────────────────────────────────────
  221 | 
  222 | test.describe('Public header (AC-017 – AC-022)', () => {
  223 |   test('AC-017/AC-018: unauthenticated visitor sees Sign In and Get Started', async ({
  224 |     page,
  225 |   }) => {
  226 |     await mockAuthMe(page, null);
  227 |     await page.goto('/', { waitUntil: 'networkidle' });
  228 | 
  229 |     // Sign In link in header (exact match to distinguish from hero CTA)
  230 |     await expect(page.getByRole('link', { name: 'Sign In' })).toBeVisible();
  231 |     // The header "Get Started" button — use exact match to avoid matching hero's "Get started free"
  232 |     await expect(page.getByRole('link', { name: 'Get Started', exact: true })).toBeVisible();
  233 |     await expect(page.getByRole('link', { name: 'Go to Dashboard' })).not.toBeVisible();
  234 |   });
  235 | 
  236 |   test('AC-019: authenticated user sees Go to Dashboard, no Sign In/Get Started', async ({
  237 |     page,
  238 |   }) => {
  239 |     await mockAuthMe(page, {
  240 |       id: '00000000-0000-0000-0000-000000000099',
  241 |       displayName: 'Jane Smith',
  242 |       email: 'jane@example.com',
  243 |     });
  244 |     await page.goto('/', { waitUntil: 'networkidle' });
  245 | 
  246 |     // Wait for auth state to resolve (useAuthUser is async via useEffect)
  247 |     await expect(page.getByRole('link', { name: 'Go to Dashboard' })).toBeVisible({ timeout: 10000 });
  248 |     await expect(page.getByRole('link', { name: 'Sign In' })).not.toBeVisible();
  249 |     // "Get Started" header button gone; hero's "Get started free" may still be present
  250 |     await expect(page.getByRole('link', { name: 'Get Started', exact: true })).not.toBeVisible();
  251 |   });
  252 | 
  253 |   test('AC-021: active nav link is highlighted on home page', async ({ page }) => {
  254 |     await mockAuthMe(page, null);
  255 |     await page.goto('/', { waitUntil: 'networkidle' });
  256 | 
  257 |     // The desktop nav link for Home should have aria-current="page"
  258 |     // The logo link (aria-label="Testurio home") is a different element; target the nav link by href
  259 |     const homeNavLink = page.locator('header a[href="/"][aria-current="page"]');
  260 |     await expect(homeNavLink).toBeAttached();
  261 |   });
  262 | 
  263 |   test('AC-022: hamburger menu is visible at 375 px viewport', async ({ page }) => {
  264 |     await mockAuthMe(page, null);
  265 |     await page.setViewportSize({ width: 375, height: 812 });
  266 |     await page.goto('/', { waitUntil: 'networkidle' });
  267 | 
  268 |     await expect(
  269 |       page.getByRole('button', { name: 'Open navigation menu' }),
  270 |     ).toBeVisible();
  271 |   });
  272 | });
  273 | 
  274 | // ─── Pricing page tests ───────────────────────────────────────────────────────
  275 | 
  276 | test.describe('Pricing page (AC-023 – AC-038)', () => {
  277 |   test.beforeEach(async ({ page }) => {
  278 |     await mockAuthMe(page, null);
  279 |     await mockPlansApi(page);
  280 |   });
  281 | 
  282 |   test('AC-023/AC-024/AC-025: four plan cards load with names and prices from API', async ({
  283 |     page,
  284 |   }) => {
  285 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  286 | 
  287 |     await expect(page.getByText('Test Junior')).toBeVisible();
  288 |     await expect(page.getByText('Test Pro')).toBeVisible();
  289 |     await expect(page.getByText('Team')).toBeVisible();
  290 |     await expect(page.getByText('Centurio')).toBeVisible();
  291 |   });
  292 | 
  293 |   test('AC-028: Test Pro card has "Most popular" badge', async ({ page }) => {
  294 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  295 | 
  296 |     await expect(page.getByText('Most popular')).toBeVisible();
  297 |   });
  298 | 
  299 |   test('AC-029/AC-030: billing interval toggle defaults to Monthly; switching to Annual updates prices', async ({
  300 |     page,
  301 |   }) => {
  302 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  303 | 
  304 |     // Monthly price visible initially (Test Pro = $49)
  305 |     await expect(page.getByText('$49')).toBeVisible();
  306 | 
  307 |     // Switch to Annual
  308 |     const annualButton = page.getByRole('button', { name: /annual/i });
  309 |     await annualButton.click();
> 310 | 
      |                                          ^ Error: expect(locator).toBeVisible() failed
  311 |     // Annual monthly-equivalent for Test Pro: 470 / 12 ≈ 39
  312 |     await expect(page.getByText('$39')).toBeVisible();
  313 |   });
  314 | 
  315 |   test('AC-031: annual price shows monthly-equivalent (annual total ÷ 12)', async ({ page }) => {
  316 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  317 | 
  318 |     // Wait for all four plans to be rendered before toggling
  319 |     await expect(page.getByText('Team')).toBeVisible();
  320 |     await expect(page.getByText('Centurio')).toBeVisible();
  321 | 
  322 |     const annualButton = page.getByRole('button', { name: /annual/i });
  323 |     await annualButton.click();
  324 | 
  325 |     // Wait for price update — Test Pro annual monthly-equivalent: 470 / 12 ≈ 39
  326 |     await expect(page.getByText('$39')).toBeVisible();
  327 | 
  328 |     // Verify other cards updated to their annual prices:
  329 |     // Team: 1430 / 12 ≈ 119; Centurio: 3830 / 12 ≈ 319
  330 |     // Use first() in case of strict mode ambiguity
  331 |     await expect(page.getByText('$119').first()).toBeVisible({ timeout: 10000 });
  332 |     await expect(page.getByText('$319').first()).toBeVisible({ timeout: 10000 });
  333 |   });
  334 | 
  335 |   test('AC-032: annual toggle shows discount badge matching annualDiscountPercent', async ({
  336 |     page,
  337 |   }) => {
  338 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  339 | 
  340 |     const annualButton = page.getByRole('button', { name: /annual/i });
  341 |     await annualButton.click();
  342 | 
  343 |     // Discount badges visible on cards (plans have 20% discount)
  344 |     const saveBadges = page.getByText('Save 20%');
  345 |     await expect(saveBadges.first()).toBeVisible();
  346 |   });
  347 | 
  348 |   test('AC-033: unauthenticated CTA redirects to /sign-up with plan and interval params', async ({
  349 |     page,
  350 |   }) => {
  351 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  352 | 
  353 |     // Wait for plans to load
  354 |     await expect(page.getByText('Test Junior')).toBeVisible();
  355 | 
  356 |     const ctaLinks = page.getByRole('link', { name: 'Get started free' });
  357 |     const firstLink = ctaLinks.first();
  358 |     const href = await firstLink.getAttribute('href');
  359 | 
  360 |     expect(href).toMatch(/sign-up/);
  361 |     expect(href).toMatch(/plan=test-junior/);
  362 |     expect(href).toMatch(/interval=monthly/);
  363 |   });
  364 | 
  365 |   test('AC-034/AC-035: authenticated user CTA shows Upgrade and links to /billing', async ({
  366 |     page,
  367 |   }) => {
  368 |     await mockAuthMe(page, {
  369 |       id: '00000000-0000-0000-0000-000000000099',
  370 |       displayName: 'Jane Smith',
  371 |       email: 'jane@example.com',
  372 |     });
  373 | 
  374 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  375 | 
  376 |     // Wait for plans to load and auth state to resolve
  377 |     await expect(page.getByText('Test Junior')).toBeVisible();
  378 |     await expect(page.getByRole('link', { name: 'Upgrade' }).first()).toBeVisible({ timeout: 10000 });
  379 | 
  380 |     const firstUpgradeLink = page.getByRole('link', { name: 'Upgrade' }).first();
  381 |     const href = await firstUpgradeLink.getAttribute('href');
  382 | 
  383 |     expect(href).toMatch(/\/billing/);
  384 |     expect(href).toMatch(/plan=/);
  385 |     expect(href).toMatch(/interval=monthly/);
  386 |   });
  387 | 
  388 |   test('AC-026: skeleton placeholders shown while loading', async ({ page }) => {
  389 |     // Override the plans route with a long delay to keep the skeleton state visible
  390 |     await page.route('**/v1/plans', async (route) => {
  391 |       await new Promise<void>((resolve) => setTimeout(resolve, 10000));
  392 |       await route.fulfill({ json: MOCK_PLANS });
  393 |     });
  394 | 
  395 |     // Use domcontentloaded so the page is ready before the delayed API resolves
  396 |     await page.goto('/pricing', { waitUntil: 'domcontentloaded' });
  397 | 
  398 |     // Immediately after JS hydrates, plan data is still loading — skeletons should be present
  399 |     const skeleton = page.locator('.MuiSkeleton-root');
  400 |     await expect(skeleton.first()).toBeAttached({ timeout: 5000 });
  401 |   });
  402 | 
  403 |   test('AC-027: error state shows inline error with "Try again" button', async ({ page }) => {
  404 |     // Override the plans route to return an error
  405 |     await page.route('**/v1/plans', (route) =>
  406 |       route.fulfill({ status: 500, body: 'Internal Server Error' }),
  407 |     );
  408 | 
  409 |     await page.goto('/pricing', { waitUntil: 'networkidle' });
  410 | 
```