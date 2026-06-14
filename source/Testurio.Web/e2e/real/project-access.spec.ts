/**
 * Project Access Mode specs
 * US-028 (IP Allowlisting), US-029 (Basic Auth), US-030 (Header Token)
 *
 * AC-115–AC-128
 */

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '../.auth/seed.json');

function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

async function navigateToAccessSection(page: import('@playwright/test').Page, seedProjectId: string) {
  await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

  // Access mode may be on the Settings tab or within Integration tab — check both
  const accessSection = page
    .locator('[data-testid="access-mode-section"]')
    .or(page.getByText(/access mode|environment access/i).first());

  if (!(await accessSection.isVisible())) {
    // Try switching to Integration tab
    const integrationTab = page.getByRole('tab', { name: /integration/i });
    if (await integrationTab.isVisible()) {
      await integrationTab.click();
    }
  }
}

// ---------------------------------------------------------------------------
// US-028 — IP Allowlisting
// ---------------------------------------------------------------------------

test.describe('Project Access Mode — IP Allowlisting', () => {
  test('access mode section has three options; IP Allowlisting hides credential fields and shows egress IPs (AC-115, AC-116, AC-117)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);

    // AC-115: three access mode options visible
    await expect(page.getByLabel(/ip allowlisting/i).or(page.getByText(/ip allowlisting/i).first())).toBeVisible({ timeout: 10_000 });
    await expect(page.getByLabel(/http basic auth/i).or(page.getByText(/http basic auth/i).first())).toBeVisible();
    await expect(page.getByLabel(/custom header token/i).or(page.getByText(/custom header token/i).first())).toBeVisible();

    // Select IP Allowlisting
    await page.getByLabel(/ip allowlisting/i).or(page.locator('input[value="ip_allowlist"]')).first().click();

    // AC-116: no credential fields
    await expect(page.locator('input[name="basicAuthUser"], input[name="headerTokenName"]')).not.toBeVisible();

    // AC-117: egress IP range displayed
    await expect(
      page.locator('[data-testid="egress-ips"]').or(page.getByText(/egress ip|nat gateway|public ip/i).first()),
    ).toBeVisible();
  });

  test('saving with IP Allowlisting selected succeeds (AC-118)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);

    await page.getByLabel(/ip allowlisting/i).or(page.locator('input[value="ip_allowlist"]')).first().click();
    await page.getByRole('button', { name: /save changes|save/i }).click();

    await expect(
      page.getByText(/saved|success/i).or(page.locator('[data-testid="save-success"]')).first(),
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

    await page.getByLabel(/http basic auth/i).or(page.locator('input[value="basic_auth"]')).first().click();

    // AC-119: Username and Password fields shown
    await expect(page.getByLabel(/username/i).or(page.locator('input[name="basicAuthUser"]'))).toBeVisible({ timeout: 5_000 });
    const passwordField = page.getByLabel(/password/i).or(page.locator('input[name="basicAuthPass"]')).first();
    await expect(passwordField).toBeVisible();

    // AC-120: Password is masked
    await expect(passwordField).toHaveAttribute('type', 'password');
  });

  test('submitting both Basic Auth fields populated succeeds (AC-121)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/http basic auth/i).or(page.locator('input[value="basic_auth"]')).first().click();

    await page.getByLabel(/username/i).or(page.locator('input[name="basicAuthUser"]')).first().fill('testuser');
    await page.getByLabel(/password/i).or(page.locator('input[name="basicAuthPass"]')).first().fill('TestPassword1!');

    await page.getByRole('button', { name: /save changes|save/i }).click();

    await expect(
      page.getByText(/saved|success/i).or(page.locator('[data-testid="save-success"]')).first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('submitting with empty Basic Auth fields shows validation errors (AC-122)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/http basic auth/i).or(page.locator('input[value="basic_auth"]')).first().click();

    await page.getByRole('button', { name: /save changes|save/i }).click();

    await expect(page.locator('[data-testid*="error"], [class*="error"], [role="alert"]').first()).toBeVisible({ timeout: 5_000 });
  });

  test('after saving, Username is pre-filled and Password field is empty (AC-123)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/http basic auth/i).or(page.locator('input[value="basic_auth"]')).first().click();

    await page.getByLabel(/username/i).or(page.locator('input[name="basicAuthUser"]')).first().fill('saveduser');
    await page.getByLabel(/password/i).or(page.locator('input[name="basicAuthPass"]')).first().fill('SavedPass1!');

    await page.getByRole('button', { name: /save changes|save/i }).click();
    await expect(
      page.getByText(/saved|success/i).or(page.locator('[data-testid="save-success"]')).first(),
    ).toBeVisible({ timeout: 10_000 });

    // Reload to verify persistence
    await page.reload({ waitUntil: 'networkidle' });
    await navigateToAccessSection(page, seedProjectId);

    // AC-123: Username pre-filled, Password empty with placeholder
    await expect(page.getByLabel(/username/i).or(page.locator('input[name="basicAuthUser"]')).first()).toHaveValue('saveduser');
    const passField = page.getByLabel(/password/i).or(page.locator('input[name="basicAuthPass"]')).first();
    await expect(passField).toHaveValue('');
    await expect(passField).toHaveAttribute('placeholder', /•+/);
  });
});

// ---------------------------------------------------------------------------
// US-030 — Custom Header Token
// ---------------------------------------------------------------------------

test.describe('Project Access Mode — Custom Header Token', () => {
  test('selecting Header Token reveals Header Name and masked Header Value fields (AC-124, AC-125)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/custom header token/i).or(page.locator('input[value="header_token"]')).first().click();

    // AC-124: Header Name and Header Value fields shown
    await expect(page.getByLabel(/header name/i).or(page.locator('input[name="headerTokenName"]'))).toBeVisible({ timeout: 5_000 });
    const valueField = page.getByLabel(/header value/i).or(page.locator('input[name="headerTokenValue"]')).first();
    await expect(valueField).toBeVisible();

    // AC-125: Header Value is masked
    await expect(valueField).toHaveAttribute('type', 'password');
  });

  test('submitting both Header Token fields populated succeeds (AC-126)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/custom header token/i).or(page.locator('input[value="header_token"]')).first().click();

    await page.getByLabel(/header name/i).or(page.locator('input[name="headerTokenName"]')).first().fill('X-Testurio-Token');
    await page.getByLabel(/header value/i).or(page.locator('input[name="headerTokenValue"]')).first().fill('test-secret-value-123');

    await page.getByRole('button', { name: /save changes|save/i }).click();

    await expect(
      page.getByText(/saved|success/i).or(page.locator('[data-testid="save-success"]')).first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('submitting with empty Header Token fields shows validation errors (AC-127)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/custom header token/i).or(page.locator('input[value="header_token"]')).first().click();

    await page.getByRole('button', { name: /save changes|save/i }).click();

    await expect(page.locator('[data-testid*="error"], [class*="error"], [role="alert"]').first()).toBeVisible({ timeout: 5_000 });
  });

  test('after saving, Header Name is pre-filled and Header Value is empty (AC-128)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await navigateToAccessSection(page, seedProjectId);
    await page.getByLabel(/custom header token/i).or(page.locator('input[value="header_token"]')).first().click();

    await page.getByLabel(/header name/i).or(page.locator('input[name="headerTokenName"]')).first().fill('X-My-Token');
    await page.getByLabel(/header value/i).or(page.locator('input[name="headerTokenValue"]')).first().fill('my-secret');

    await page.getByRole('button', { name: /save changes|save/i }).click();
    await expect(
      page.getByText(/saved|success/i).or(page.locator('[data-testid="save-success"]')).first(),
    ).toBeVisible({ timeout: 10_000 });

    await page.reload({ waitUntil: 'networkidle' });
    await navigateToAccessSection(page, seedProjectId);

    // AC-128: Header Name pre-filled, Header Value empty with placeholder
    await expect(page.getByLabel(/header name/i).or(page.locator('input[name="headerTokenName"]')).first()).toHaveValue('X-My-Token');
    const valueField = page.getByLabel(/header value/i).or(page.locator('input[name="headerTokenValue"]')).first();
    await expect(valueField).toHaveValue('');
    await expect(valueField).toHaveAttribute('placeholder', /•+/);
  });
});
