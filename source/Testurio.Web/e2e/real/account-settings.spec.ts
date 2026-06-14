/**
 * Account Settings specs — US-040 (Display Name), US-041 (Language & Theme)
 *
 * AC-169–AC-177
 */

import { test, expect } from '@playwright/test';

// ---------------------------------------------------------------------------
// US-040 — Account Settings — Update Display Name
// ---------------------------------------------------------------------------

test.describe('Account Settings — Display Name', () => {
  test('Personal Information section is visible with pre-populated Display Name (AC-169, AC-170)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    // AC-169: Personal Information section visible
    await expect(page.getByText(/personal information/i).first()).toBeVisible({ timeout: 10_000 });

    // AC-170: Display Name field is pre-populated (not empty)
    const displayNameField = page
      .getByLabel(/display name/i)
      .or(page.locator('input[name="displayName"]'))
      .first();
    await expect(displayNameField).toBeVisible();
    const value = await displayNameField.inputValue();
    expect(value.length).toBeGreaterThan(0);
  });

  test('updating display name and saving shows success snackbar and updates header (AC-171, AC-172)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    const displayNameField = page
      .getByLabel(/display name/i)
      .or(page.locator('input[name="displayName"]'))
      .first();

    const originalName = await displayNameField.inputValue();
    const newName = `E2E Tester ${Date.now()}`;

    await displayNameField.fill(newName);

    // AC-171: clicking Save calls PATCH /v1/account/profile
    const [apiReq] = await Promise.all([
      page.waitForRequest((req) =>
        req.method() === 'PATCH' && req.url().includes('/v1/account/profile'),
      ),
      page.getByRole('button', { name: /save/i }).click(),
    ]);

    expect(apiReq).toBeTruthy();

    // Success snackbar shown
    await expect(
      page.getByText(/settings saved/i).or(page.locator('[role="alert"]').filter({ hasText: /saved|success/i })),
    ).toBeVisible({ timeout: 10_000 });

    // AC-172: header reflects updated display name without full reload
    await expect(
      page.getByRole('banner').getByText(newName).or(
        page.getByRole('banner').locator('[data-testid="user-identity"]', { hasText: newName }),
      ),
    ).toBeVisible({ timeout: 10_000 });

    // Restore original name to avoid polluting other tests
    await displayNameField.fill(originalName);
    await page.getByRole('button', { name: /save/i }).click();
    await page.waitForResponse((res) => res.url().includes('/v1/account/profile') && res.status() === 200);
  });

  test('empty Display Name shows validation error (AC-173)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    const displayNameField = page
      .getByLabel(/display name/i)
      .or(page.locator('input[name="displayName"]'))
      .first();

    await displayNameField.clear();
    await page.getByRole('button', { name: /save/i }).click();

    // AC-173: validation error shown
    await expect(
      page.getByText(/display name is required/i),
    ).toBeVisible({ timeout: 5_000 });
  });
});

// ---------------------------------------------------------------------------
// US-041 — Account Settings — Language and Theme Preferences
// ---------------------------------------------------------------------------

test.describe('Account Settings — Language and Theme', () => {
  test('Preferences section has Language dropdown with en and uk options (AC-174, AC-175)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    // AC-174: Preferences section visible
    await expect(page.getByText(/preferences/i).first()).toBeVisible({ timeout: 10_000 });

    // Language dropdown
    const languageDropdown = page
      .getByLabel(/language/i)
      .or(page.locator('select[name="language"], [data-testid="language-selector"]'))
      .first();
    await expect(languageDropdown).toBeVisible();

    // AC-175: at least en and uk options
    const options = await languageDropdown.locator('option').allTextContents();
    const hasEnglish = options.some((o) => /english|en/i.test(o));
    const hasUkrainian = options.some((o) => /ukrainian|uk/i.test(o));
    expect(hasEnglish).toBe(true);
    expect(hasUkrainian).toBe(true);
  });

  test('Appearance toggle (Light/Dark) is visible (AC-174)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    const appearanceToggle = page
      .getByRole('group', { name: /appearance/i })
      .or(page.locator('[data-testid="appearance-toggle"], [aria-label*="appearance"]'))
      .or(page.getByLabel(/appearance/i))
      .first();

    await expect(appearanceToggle).toBeVisible({ timeout: 10_000 });
  });

  test('selecting Dark theme applies dark theme immediately (AC-176)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    const darkToggle = page.getByRole('button', { name: /dark/i });
    await expect(darkToggle).toBeVisible({ timeout: 10_000 });
    await darkToggle.click();

    // AC-176: dark theme applied immediately (data-theme or class on html/body)
    const themeProp = await page.evaluate(() => {
      const html = document.documentElement;
      return (
        html.getAttribute('data-theme') ??
        html.getAttribute('data-color-scheme') ??
        html.className
      );
    });
    expect(themeProp).toMatch(/dark/);

    // Restore light theme
    await page.getByRole('button', { name: /light/i }).click();
  });

  test('Dark theme preference persists after page refresh (AC-177)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'networkidle' });

    const darkToggle = page.getByRole('button', { name: /dark/i });
    await darkToggle.click();

    // Save preferences
    const saveButton = page.getByRole('button', { name: /save preferences|save/i });
    if (await saveButton.isVisible()) {
      await saveButton.click();
      await page.waitForResponse((res) =>
        res.url().includes('/v1/account') && (res.status() === 200 || res.status() === 204),
      );
    }

    // AC-177: refresh and dark theme is still applied
    await page.reload({ waitUntil: 'networkidle' });

    const themeProp = await page.evaluate(() => {
      const html = document.documentElement;
      return (
        html.getAttribute('data-theme') ??
        html.getAttribute('data-color-scheme') ??
        html.className
      );
    });
    expect(themeProp).toMatch(/dark/);

    // Restore light theme
    await page.goto('/settings', { waitUntil: 'networkidle' });
    await page.getByRole('button', { name: /light/i }).click();
    const restoreSave = page.getByRole('button', { name: /save preferences|save/i });
    if (await restoreSave.isVisible()) {
      await restoreSave.click();
    }
  });
});
