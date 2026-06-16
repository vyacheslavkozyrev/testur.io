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

import { test, expect, type APIRequestContext } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '../.auth/seed.json');

/**
 * Resolve the seed project ID.
 * 1. Try seed.json + live project check.
 * 2. Fall back to listing /v1/projects and finding [E2E] Seed Project.
 * 3. Create a new seed project if neither is found.
 *
 * Writes seed.json on creation so the teardown can clean up.
 */
async function ensureSeedProject(request: APIRequestContext): Promise<string> {
  if (fs.existsSync(seedFile)) {
    const { projectId } = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
    const check = await request.get(`/v1/projects/${projectId}`);
    if (check.ok()) return projectId;
  }

  const listRes = await request.get('/v1/projects');
  if (listRes.ok()) {
    const projects = (await listRes.json()) as Array<{ projectId: string; name: string }>;
    const seed = projects.find((p) => p.name === '[E2E] Seed Project');
    if (seed) {
      fs.mkdirSync(path.dirname(seedFile), { recursive: true });
      fs.writeFileSync(seedFile, JSON.stringify({ projectId: seed.projectId }, null, 2));
      return seed.projectId;
    }
  }

  const createRes = await request.post('/v1/projects', {
    data: {
      name: '[E2E] Seed Project',
      productUrl: 'https://example.com',
      testingStrategy: 'Automated E2E seed project — do not delete manually.',
      requestTimeoutSeconds: 30,
    },
  });
  if (!createRes.ok()) {
    throw new Error(`Failed to create seed project: ${createRes.status()} ${await createRes.text()}`);
  }
  const body = (await createRes.json()) as { projectId: string };
  fs.mkdirSync(path.dirname(seedFile), { recursive: true });
  fs.writeFileSync(seedFile, JSON.stringify({ projectId: body.projectId }, null, 2));
  return body.projectId;
}

/**
 * Reset the seed project's access mode back to IpAllowlist via API.
 * Called in afterEach for any describe block that writes access credentials.
 */
async function resetToIpAllowlist(request: APIRequestContext, projectId: string): Promise<void> {
  await request.patch(`/v1/projects/${projectId}/access`, {
    data: { accessMode: 'IpAllowlist' },
  });
}

/**
 * Navigate to the project settings page (Settings tab, which is the default).
 * The AccessModeSelector is rendered on the Settings tab inside the
 * "Testing Environment Access" card. Its radiogroup has
 * aria-label="Environment access method".
 */
async function navigateToAccessSection(page: import('@playwright/test').Page, projectId: string) {
  await page.goto(`/projects/${projectId}/settings`, { waitUntil: 'load' });

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
  let seedProjectId: string;

  test.beforeAll(async ({ request }) => {
    seedProjectId = await ensureSeedProject(request);
  });

  test('access mode section has three options; IP Allowlisting hides credential fields and shows egress IPs (AC-115, AC-116, AC-117)', async ({ page }) => {
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
  let seedProjectId: string;

  test.beforeAll(async ({ request }) => {
    seedProjectId = await ensureSeedProject(request);
  });

  test.afterEach(async ({ request }) => {
    // Reset to IpAllowlist so that subsequent tests start from a clean state
    await resetToIpAllowlist(request, seedProjectId);
  });

  test('selecting Basic Auth reveals masked Username and Password fields (AC-119, AC-120)', async ({ page }) => {
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
    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /http basic auth/i).click();

    await accessTextbox(page, /^username$/i).fill('e2e-user');
    await page.locator('input[type="password"]').first().fill('e2e-pass');

    await page.getByRole('button', { name: /save changes/i }).click();
    await expect(page.getByRole('button', { name: /saved/i })).toBeVisible({ timeout: 10_000 });
  });

  test('submitting with empty Basic Auth fields shows validation errors (AC-122)', async ({ page }) => {
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
    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /http basic auth/i).click();

    await accessTextbox(page, /^username$/i).fill('e2e-user');
    await page.locator('input[type="password"]').first().fill('e2e-pass');
    await page.getByRole('button', { name: /save changes/i }).click();
    await expect(page.getByRole('button', { name: /saved/i })).toBeVisible({ timeout: 10_000 });

    // Reload and verify username pre-filled, password empty
    await page.reload({ waitUntil: 'load' });
    await expect(accessTextbox(page, /^username$/i)).toHaveValue('e2e-user');
    await expect(page.locator('input[type="password"]').first()).toHaveValue('');
  });
});

// ---------------------------------------------------------------------------
// US-030 — Custom Header Token
// ---------------------------------------------------------------------------

test.describe('Project Access Mode — Custom Header Token', () => {
  let seedProjectId: string;

  test.beforeAll(async ({ request }) => {
    seedProjectId = await ensureSeedProject(request);
  });

  test.afterEach(async ({ request }) => {
    // Reset to IpAllowlist so that subsequent tests start from a clean state
    await resetToIpAllowlist(request, seedProjectId);
  });

  test('selecting Header Token reveals Header Name and masked Header Value fields (AC-124, AC-125)', async ({ page }) => {
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
    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /custom header token/i).click();

    await accessTextbox(page, /^header name$/i).fill('X-E2E-Token');
    await page.locator('input[type="password"]').first().fill('e2e-secret-value');

    await page.getByRole('button', { name: /save changes/i }).click();
    await expect(page.getByRole('button', { name: /saved/i })).toBeVisible({ timeout: 10_000 });
  });

  test('submitting with empty Header Token fields shows validation errors (AC-127)', async ({ page }) => {
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
    await navigateToAccessSection(page, seedProjectId);
    await accessRadio(page, /custom header token/i).click();

    await accessTextbox(page, /^header name$/i).fill('X-E2E-Token');
    await page.locator('input[type="password"]').first().fill('e2e-secret-value');
    await page.getByRole('button', { name: /save changes/i }).click();
    await expect(page.getByRole('button', { name: /saved/i })).toBeVisible({ timeout: 10_000 });

    // Reload and verify header name pre-filled, value empty
    await page.reload({ waitUntil: 'load' });
    await expect(accessTextbox(page, /^header name$/i)).toHaveValue('X-E2E-Token');
    await expect(page.locator('input[type="password"]').first()).toHaveValue('');
  });
});
