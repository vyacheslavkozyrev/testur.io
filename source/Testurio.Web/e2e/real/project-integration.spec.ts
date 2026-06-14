/**
 * Project Integration Tab specs
 * US-025 (Integration Tab), US-026 (ADO Form), US-027 (Jira Form)
 *
 * AC-103–AC-114
 */

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '../.auth/seed.json');

function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

// ---------------------------------------------------------------------------
// US-025 — Integration Tab Accessible
// ---------------------------------------------------------------------------

test.describe('Project Settings — Integration Tab', () => {
  test('Integration tab renders without page error and has PM tool selector (AC-103, AC-104)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    // Click the Integration tab
    await page.getByRole('tab', { name: /integration/i }).click();

    // AC-103: no page error
    await expect(page.locator('[data-testid="error-boundary"], [data-testid="error-page"]')).not.toBeVisible();

    // AC-104: PM tool type selector present
    await expect(
      page
        .getByLabel(/pm tool|project management tool/i)
        .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
        .first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('switching between Settings and Integration tabs is client-side (AC-105)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    let navigationCount = 0;
    page.on('framenavigated', () => { navigationCount++; });
    // Reset count after initial load
    navigationCount = 0;

    await page.getByRole('tab', { name: /integration/i }).click();
    await page.getByRole('tab', { name: /settings/i }).click();

    // AC-105: No full page navigations (tabs switch client-side)
    expect(navigationCount).toBe(0);
  });

  // ---------------------------------------------------------------------------
  // US-026 — ADO Form
  // ---------------------------------------------------------------------------

  test('selecting ADO reveals ADO-specific form fields and PAT auth method (AC-107, AC-108)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });
    await page.getByRole('tab', { name: /integration/i }).click();

    // Select Azure DevOps
    const pmToolSelector = page
      .getByLabel(/pm tool|project management tool/i)
      .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
      .first();

    await pmToolSelector.selectOption('ado');

    // AC-107: ADO-specific fields visible
    await expect(page.getByLabel(/organization url/i).or(page.locator('input[name="organizationUrl"]'))).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/project name/i).or(page.locator('input[name="adoProjectName"]'))).toBeVisible();
    await expect(page.getByLabel(/team/i).or(page.locator('input[name="team"]'))).toBeVisible();
    await expect(page.getByLabel(/in testing.*status|status name/i).or(page.locator('input[name="inTestingStatus"]'))).toBeVisible();

    // Select PAT auth method
    const authMethodSelector = page
      .getByLabel(/auth method/i)
      .or(page.locator('select[name="authMethod"]'))
      .first();
    await authMethodSelector.selectOption('pat');

    // AC-108: PAT token field appears
    await expect(page.getByLabel(/pat token|personal access token/i).or(page.locator('input[name="patToken"]'))).toBeVisible({ timeout: 5_000 });
  });

  test('submitting empty ADO required fields shows validation errors (AC-110)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });
    await page.getByRole('tab', { name: /integration/i }).click();

    const pmToolSelector = page
      .getByLabel(/pm tool|project management tool/i)
      .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
      .first();
    await pmToolSelector.selectOption('ado');

    await page.getByRole('button', { name: /save|connect/i }).click();

    // AC-110: at least one validation error shown
    await expect(page.locator('[data-testid*="error"], [class*="error"], [role="alert"]').first()).toBeVisible({ timeout: 5_000 });
  });

  // ---------------------------------------------------------------------------
  // US-027 — Jira Form
  // ---------------------------------------------------------------------------

  test('selecting Jira reveals Jira-specific form fields (AC-111)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });
    await page.getByRole('tab', { name: /integration/i }).click();

    const pmToolSelector = page
      .getByLabel(/pm tool|project management tool/i)
      .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
      .first();
    await pmToolSelector.selectOption('jira');

    // AC-111: Jira-specific fields visible
    await expect(page.getByLabel(/base url/i).or(page.locator('input[name="jiraBaseUrl"]'))).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/project key/i).or(page.locator('input[name="jiraProjectKey"]'))).toBeVisible();
    await expect(page.getByLabel(/in testing.*status|status name/i).or(page.locator('input[name="inTestingStatus"]'))).toBeVisible();
  });

  test('selecting API Token + Email auth reveals Email and Token fields (AC-112)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });
    await page.getByRole('tab', { name: /integration/i }).click();

    const pmToolSelector = page
      .getByLabel(/pm tool|project management tool/i)
      .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
      .first();
    await pmToolSelector.selectOption('jira');

    const authMethodSelector = page
      .getByLabel(/auth method/i)
      .or(page.locator('select[name="authMethod"]'))
      .first();
    await authMethodSelector.selectOption('apiTokenEmail');

    // AC-112: Email and API Token fields both visible
    await expect(page.getByLabel(/email/i).or(page.locator('input[name="jiraEmail"]'))).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/api token/i).or(page.locator('input[name="jiraApiToken"]'))).toBeVisible();
  });

  test('selecting PAT for Jira hides Email field (AC-113)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });
    await page.getByRole('tab', { name: /integration/i }).click();

    const pmToolSelector = page
      .getByLabel(/pm tool|project management tool/i)
      .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
      .first();
    await pmToolSelector.selectOption('jira');

    const authMethodSelector = page
      .getByLabel(/auth method/i)
      .or(page.locator('select[name="authMethod"]'))
      .first();
    await authMethodSelector.selectOption('pat');

    // AC-113: PAT field visible, Email field hidden
    await expect(page.getByLabel(/pat token|personal access token/i).or(page.locator('input[name="patToken"]'))).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('input[name="jiraEmail"]')).not.toBeVisible();
  });

  test('submitting empty Jira required fields shows validation errors (AC-114)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });
    await page.getByRole('tab', { name: /integration/i }).click();

    const pmToolSelector = page
      .getByLabel(/pm tool|project management tool/i)
      .or(page.locator('select[name="pmTool"], [data-testid="pm-tool-selector"]'))
      .first();
    await pmToolSelector.selectOption('jira');

    await page.getByRole('button', { name: /save|connect/i }).click();

    // AC-114: at least one validation error shown
    await expect(page.locator('[data-testid*="error"], [class*="error"], [role="alert"]').first()).toBeVisible({ timeout: 5_000 });
  });
});
