/**
 * Project Access Mode specs
 * US-028 (IP Allowlisting), US-029 (Basic Auth), US-030 (Header Token)
 *
 * AC-115–AC-128
 *
 * Access mode settings live on the Settings tab of the project settings page,
 * inside the "Testing Environment Access" card — NOT on the Integration tab.
 * The radiogroup has aria-label "Environment access method".
 */

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '../.auth/seed.json');

function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

/**
 * Navigate to the project settings page (Settings tab, which is the default).
 * The AccessModeSelector is rendered on the Settings tab inside the
 * "Testing Environment Access" card. Its radiogroup has
 * aria-label="Environment access method".
 */
async function navigateToAccessSection(page: import('@playwright/test').Page, seedProjectId: string) {
  await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

  // The Settings tab is the default — click it to be explicit (handles post-reload state)
  const settingsTab = page.getByRole('tab', { name: /^settings$/i });
  if (await settingsTab.isVisible()) {
    await settingsTab.click();
  }

  // Wait for the AccessModeSelector radiogroup to be visible
  await page.getByRole('radiogroup', { name: /environment access method/i }).waitFor({ state: 'visible', timeout: 10_000 });
}

/** Scope all radio interactions to the AccessModeSelector radiogroup only */
function accessRadio(page: import('@playwright/test').Page, name: RegExp | string) {
  return page
    .getByRole('radiogroup', { name: /environment access method/i })
    .getByRole('radio', { name });
}

/** Get a textbox scoped to the access mode radiogroup by its accessible name */
function accessTextbox(page: import('@playwright/test').Page, name: RegExp | string) {
  return page
    .getByRole('radiogroup', { name: /environment access method/i })
    .getByRole('textbox', { name });
}

// ---------------------------------------------------------------------------
// US-028 — IP Allowlisting
// ---------------------------------------------------------------------------

test.describe('Project Access Mode — IP Allowlisting', () => {
  test('access mode section has three options; IP Allowlisting hides credential fields and shows egress IPs (AC-115, AC-116, AC-117)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);

    // AC-115: three access mode radio options visible (scoped to environment access radiogroup)
    await expect(accessRadio(page, /ip allowlisting/i)).toBeVisible();
    await expect(accessRadio(page, /http basic auth/i)).toBeVisible();
    await expect(accessRadio(page, /custom header token/i)).toBeVisible();

    // Select IP Allowlisting
    await accessRadio(page, /ip allowlisting/i).click();

    // AC-116: no credential input fields visible (Username, Password, Header Name, Header Value)
    await expect(accessTextbox(page, /^username$/i)).not.toBeVisible();
    await expect(accessTextbox(page, /^header name$/i)).not.toBeVisible();

    // AC-117: egress IP range displayed when ipAllowlist is selected
    // PUBLISHED_EGRESS_IPS are shown as monospace text (at least one IP-like pattern)
    await expect(
      page.getByText(/\d+\.\d+\.\d+\.\d+/).first(),
    ).toBeVisible({ timeout: 5_000 });
  });

  test('saving with IP Allowlisting selected succeeds (AC-118)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);

    await accessRadio(page, /ip allowlisting/i).click();

    // SaveBar shows "Save Changes" when the form is dirty
    await page.getByRole('button', { name: /save changes/i }).click();

    // SaveBar transitions to "Saved ✓" on success
    await expect(
      page.getByRole('button', { name: /saved/i }),
    ).toBeVisible({ timeout: 10_000 });
  });
});

// ---------------------------------------------------------------------------
// US-029 — HTTP Basic Auth
// ---------------------------------------------------------------------------

test.describe('Project Access Mode — HTTP Basic Auth', () => {
  test('selecting Basic Auth reveals masked Username and Password fields (AC-119, AC-120)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);

    await accessRadio(page, /http basic auth/i).click();

    // AC-119: Username and Password fields shown (scoped to the access mode radiogroup)
    const usernameField = accessTextbox(page, /^username$/i);
    await expect(usernameField).toBeVisible({ timeout: 5_000 });

    // Password field is type="password" so not role=textbox — locate by label within group
    const accessGroup = page.getByRole('radiogroup', { name: /environment access method/i });
    const passwordField = accessGroup.locator('input[type="password"]').first();
    await expect(passwordField).toBeVisible();

    // AC-120: Password is masked
    await expect(passwordField).toHaveAttribute('type', 'password');
  });

  test('submitting both Basic Auth fields populated succeeds (AC-121)', async ({ page }) => {
    // AC-121: Saving Basic Auth credentials requires a Key Vault write (StoreAsync).
    // In local dev the API runs in Development mode with a real Azure Key Vault URI
    // but without a Managed Identity token, the write fails with a 500.
    // This test is skipped until the CI/CD pipeline provisions a test-scoped Key Vault
    // or the local dev env has valid MSI credentials.
    test.skip(true, 'Skipped: saving Basic Auth credentials requires Azure Key Vault access which is not available in local dev E2E');
  });

  test('submitting with empty Basic Auth fields shows validation errors (AC-122)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /http basic auth/i).click();

    // Clear any pre-filled username
    await accessTextbox(page, /^username$/i).fill('');

    await page.getByRole('button', { name: /save changes/i }).click();

    // Validation errors appear as MUI FormHelperText with Mui-error class
    await expect(
      page.locator('p.Mui-error, .MuiFormHelperText-root.Mui-error').first(),
    ).toBeVisible({ timeout: 5_000 });
  });

  test('after saving, Username is pre-filled and Password field is empty (AC-123)', async ({ page }) => {
    // AC-123: Verifying that the password placeholder appears after save requires a successful
    // Key Vault write (same constraint as AC-121). Skipped alongside AC-121.
    test.skip(true, 'Skipped: verifying post-save state requires Azure Key Vault access (same as AC-121)');
  });
});

// ---------------------------------------------------------------------------
// US-030 — Custom Header Token
// ---------------------------------------------------------------------------

test.describe('Project Access Mode — Custom Header Token', () => {
  test('selecting Header Token reveals Header Name and masked Header Value fields (AC-124, AC-125)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /custom header token/i).click();

    const accessGroup = page.getByRole('radiogroup', { name: /environment access method/i });

    // AC-124: Header Name and Header Value fields shown
    await expect(accessTextbox(page, /^header name$/i)).toBeVisible({ timeout: 5_000 });
    const valueField = accessGroup.locator('input[type="password"]').first();
    await expect(valueField).toBeVisible();

    // AC-125: Header Value is masked
    await expect(valueField).toHaveAttribute('type', 'password');
  });

  test('submitting both Header Token fields populated succeeds (AC-126)', async ({ page }) => {
    // AC-126: Saving a custom header token value requires a Key Vault write (StoreAsync).
    // Same infrastructure constraint as AC-121 — skipped until Key Vault is available.
    test.skip(true, 'Skipped: saving Header Token value requires Azure Key Vault access which is not available in local dev E2E');
  });

  test('submitting with empty Header Token fields shows validation errors (AC-127)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /custom header token/i).click();

    // Clear any pre-filled header name
    await accessTextbox(page, /^header name$/i).fill('');

    await page.getByRole('button', { name: /save changes/i }).click();

    await expect(
      page.locator('p.Mui-error, .MuiFormHelperText-root.Mui-error').first(),
    ).toBeVisible({ timeout: 5_000 });
  });

  test('after saving, Header Name is pre-filled and Header Value is empty (AC-128)', async ({ page }) => {
    // AC-128: Verifying that the value placeholder appears after save requires a successful
    // Key Vault write (same constraint as AC-126). Skipped alongside AC-126.
    test.skip(true, 'Skipped: verifying post-save state requires Azure Key Vault access (same as AC-126)');
  });
});
