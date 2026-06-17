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
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    // AC-169: Personal Information section visible.
    // PersonalInfoSection renders <Typography variant="h6">{t('personalInfo.title')}</Typography>
    // where the translation key 'personalInfo.title' = "Personal Information".
    await expect(page.getByText(/personal information/i).first()).toBeVisible({ timeout: 10_000 });

    // AC-170: Name field is pre-populated (not empty).
    // PersonalInfoSection uses "First Name" + "Last Name" fields (not a single "Display Name").
    // The E2E seed user has firstName = "E2E", lastName = "Test".
    // We check "First Name" as the pre-populated identity field.
    const firstNameField = page.getByLabel(/first name/i).first();
    await expect(firstNameField).toBeVisible({ timeout: 10_000 });
    const value = await firstNameField.inputValue();
    expect(value.length).toBeGreaterThan(0);
  });

  test('updating display name and saving shows success snackbar and updates header (AC-171, AC-172)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    // PersonalInfoSection uses "First Name" field (not a single "Display Name").
    const firstNameField = page.getByLabel(/first name/i).first();
    await expect(firstNameField).toBeVisible({ timeout: 10_000 });

    const originalName = await firstNameField.inputValue();
    const newFirstName = `E2E-${Date.now()}`;

    await firstNameField.fill(newFirstName);

    // AC-171: clicking Save calls PATCH /v1/account/profile
    const [apiReq] = await Promise.all([
      page.waitForRequest((req) =>
        req.method() === 'PATCH' && req.url().includes('/v1/account/profile'),
      ),
      page.getByRole('button', { name: /^save$/i }).first().click(),
    ]);

    expect(apiReq).toBeTruthy();

    // Success snackbar shown (AccountSettingsPage shows "Settings saved" on success).
    // Use role="alert" to avoid strict-mode violation (MUI Alert renders both a root div
    // with role="alert" and an inner div with the text — the .first() picks the outer).
    await expect(
      page.getByRole('alert').filter({ hasText: /settings saved/i }).first(),
    ).toBeVisible({ timeout: 10_000 });

    // AC-172: header reflects updated name without full reload.
    // AppHeader.getDisplayLabel joins firstName + lastName.
    await expect(
      page.getByRole('banner').getByText(new RegExp(newFirstName, 'i')),
    ).toBeVisible({ timeout: 10_000 });

    // Restore original name to avoid polluting other tests
    await firstNameField.fill(originalName);
    await page.getByRole('button', { name: /^save$/i }).first().click();
    await page.waitForResponse((res) => res.url().includes('/v1/account/profile') && res.status() === 200);
  });

  test('empty First Name shows validation error (AC-173)', async ({ page }) => {
    await page.goto('/settings', { waitUntil: 'load' });

    const firstNameField = page.getByLabel(/first name/i).first();
    await expect(firstNameField).toBeVisible({ timeout: 10_000 });

    await firstNameField.clear();
    await page.getByRole('button', { name: /^save$/i }).first().click();

    await expect(page.getByText(/first name is required/i)).toBeVisible({ timeout: 5_000 });
  });
});

// ---------------------------------------------------------------------------
// US-041 — Account Settings — Language and Theme Preferences
// ---------------------------------------------------------------------------

test.describe('Account Settings — Language and Theme', () => {
  test('Preferences section has Language dropdown with en and uk options (AC-174, AC-175)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    // AC-174: Preferences section visible
    await expect(page.getByText(/preferences/i).first()).toBeVisible({ timeout: 10_000 });

    // The Language field is a MUI Select (combobox), not a native <select>.
    // Playwright sees it as role="combobox" with accessible label "Language".
    const languageDropdown = page.getByRole('combobox', { name: /language/i });
    await expect(languageDropdown).toBeVisible({ timeout: 10_000 });

    // AC-175: Open the dropdown to inspect the options rendered as MUI MenuItems.
    await languageDropdown.click();

    // MUI renders the open dropdown in a Portal (outside the combobox DOM subtree).
    // Wait for the listbox to appear.
    const listbox = page.getByRole('listbox');
    await expect(listbox).toBeVisible({ timeout: 5_000 });

    const options = await listbox.getByRole('option').allTextContents();
    // PreferencesSection SUPPORTED_LANGUAGES labels: 'English', 'Español', 'Українська', 'Беларуская'.
    // 'uk' value maps to the Cyrillic label 'Українська' — match on that string.
    const hasEnglish = options.some((o) => /english/i.test(o));
    const hasUkrainian = options.some((o) => /Українська/.test(o));
    expect(hasEnglish).toBe(true);
    expect(hasUkrainian).toBe(true);

    // Close dropdown by pressing Escape
    await page.keyboard.press('Escape');
  });

  test('Appearance toggle (Light/Dark) is visible (AC-174)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    // PreferencesSection renders a MUI ToggleButtonGroup with aria-label="Appearance"
    // and two ToggleButtons: "Light" and "Dark".
    const lightButton = page.getByRole('button', { name: /^light$/i });
    const darkButton = page.getByRole('button', { name: /^dark$/i });

    await expect(lightButton).toBeVisible({ timeout: 10_000 });
    await expect(darkButton).toBeVisible({ timeout: 10_000 });
  });

  test('selecting Dark theme applies dark theme immediately (AC-176)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    const darkToggle = page.getByRole('button', { name: /^dark$/i });
    await expect(darkToggle).toBeVisible({ timeout: 10_000 });
    await darkToggle.click();

    // AC-176: dark theme applied immediately.
    // ThemeContextProvider stores the selection in localStorage ('testurio.theme' = 'dark')
    // and re-creates the MUI theme. The html element does NOT receive data-theme or a class
    // — MUI manages theme solely via React context and CSS-in-JS.
    // We verify localStorage as the authoritative signal that the theme was switched.
    const storedTheme = await page.evaluate(() => localStorage.getItem('testurio.theme'));
    expect(storedTheme).toBe('dark');

    // Additionally verify the Dark toggle button is now pressed (aria-pressed="true")
    await expect(darkToggle).toHaveAttribute('aria-pressed', 'true');

    // Restore light theme
    await page.getByRole('button', { name: /^light$/i }).click();
    await page.evaluate(() => localStorage.setItem('testurio.theme', 'light'));
  });

  test('Dark theme preference persists after page refresh (AC-177)', async ({ page }) => {
    // waitUntil: 'load' — 'networkidle' times out due to SSE stream on dashboard.
    await page.goto('/settings', { waitUntil: 'load' });

    const darkToggle = page.getByRole('button', { name: /^dark$/i });
    await expect(darkToggle).toBeVisible({ timeout: 10_000 });
    await darkToggle.click();

    // Verify localStorage captured the preference
    const storedBefore = await page.evaluate(() => localStorage.getItem('testurio.theme'));
    expect(storedBefore).toBe('dark');

    // AC-177: reload — ThemeContextProvider reads localStorage in readStoredTheme()
    // and restores 'dark' on mount.
    await page.reload({ waitUntil: 'load' });

    const storedAfter = await page.evaluate(() => localStorage.getItem('testurio.theme'));
    expect(storedAfter).toBe('dark');

    // Dark toggle should still be pressed after reload
    const darkToggleAfter = page.getByRole('button', { name: /^dark$/i });
    await expect(darkToggleAfter).toBeVisible({ timeout: 10_000 });
    await expect(darkToggleAfter).toHaveAttribute('aria-pressed', 'true');

    // Restore light theme
    await page.getByRole('button', { name: /^light$/i }).click();
    await page.evaluate(() => localStorage.setItem('testurio.theme', 'light'));
  });
});
