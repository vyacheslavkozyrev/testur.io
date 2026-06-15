/**
 * Project Integration Tab specs
 * US-025 (Integration Tab), US-026 (ADO Form), US-027 (Jira Form)
 *
 * AC-103–AC-114
 *
 * Uses a dedicated [E2E] Integration Test project (not the seed project) so
 * these tests always start without a PM tool configured.  The project is
 * created in beforeAll and deleted in afterAll to keep the environment clean.
 */

import { test, expect } from '@playwright/test';

// ---------------------------------------------------------------------------
// US-025 — Integration Tab Accessible
// ---------------------------------------------------------------------------

test.describe('Project Settings — Integration Tab', () => {
  let integrationProjectId: string;

  test.beforeAll(async ({ request }) => {
    const res = await request.post('/v1/projects', {
      data: {
        name: '[E2E] Integration Test',
        productUrl: 'https://integration-test.example.com',
        testingStrategy: 'Temporary project — created for integration form tests.',
        requestTimeoutSeconds: 30,
      },
    });
    expect(res.ok()).toBeTruthy();
    const body = (await res.json()) as { projectId: string };
    integrationProjectId = body.projectId;
  });

  test.afterAll(async ({ request }) => {
    if (integrationProjectId) {
      await request.delete(`/v1/projects/${integrationProjectId}`).catch(() => {});
    }
  });

  test('Integration tab renders without page error and has PM tool buttons (AC-103, AC-104)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });

    // Click the Integration tab
    await page.getByRole('tab', { name: /^integration$/i }).click();

    // AC-103: no page error — integration page loads without error boundary
    await expect(page.locator('[data-testid="error-boundary"], [data-testid="error-page"]')).not.toBeVisible();

    // AC-104: PM tool connection UI present.
    // When no integration is configured, "Connect Azure DevOps" and "Connect Jira"
    // buttons are shown. When already configured, a status card is shown instead.
    const connectAdoButton = page.getByRole('button', { name: /connect azure devops/i });
    const connectJiraButton = page.getByRole('button', { name: /connect jira/i });
    const statusCard = page.getByText(/pm tool|azure devops|jira/i).first();

    // At least one of these must be visible
    await expect(connectAdoButton.or(connectJiraButton).or(statusCard).first()).toBeVisible({ timeout: 10_000 });
  });

  test('switching between Settings and Integration tabs is client-side (AC-105)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    const baseUrl = page.url();

    await page.getByRole('tab', { name: /^integration$/i }).click();
    await page.getByRole('tab', { name: /^settings$/i }).click();

    // AC-105: URL stays on the same settings page — no hard navigation to a new page
    expect(page.url()).toBe(baseUrl);
  });

  // ---------------------------------------------------------------------------
  // US-026 — ADO Form
  // ---------------------------------------------------------------------------

  test('clicking Connect Azure DevOps reveals ADO-specific form fields (AC-107, AC-108)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    await page.getByRole('tab', { name: /^integration$/i }).click();

    const connectAdoButton = page.getByRole('button', { name: /connect azure devops/i });
    await expect(connectAdoButton).toBeVisible({ timeout: 10_000 });
    await connectAdoButton.click();

    // AC-107: ADO-specific fields visible
    await expect(page.getByLabel(/organization url/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/project name/i)).toBeVisible();
    await expect(page.getByLabel(/^team$/i)).toBeVisible();
    await expect(page.getByLabel(/"in testing" status name/i)).toBeVisible();

    // AC-108: auth method selector present and PAT field visible (PAT is default)
    await expect(page.getByLabel(/auth method/i)).toBeVisible();
    // PAT is the default auth method, so the PAT field should already be visible
    await expect(page.getByLabel(/personal access token/i)).toBeVisible({ timeout: 5_000 });
  });

  test('submitting empty ADO required fields shows validation errors (AC-110)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    await page.getByRole('tab', { name: /^integration$/i }).click();

    const connectAdoButton = page.getByRole('button', { name: /connect azure devops/i });
    await expect(connectAdoButton).toBeVisible({ timeout: 10_000 });
    await connectAdoButton.click();

    // Submit empty form
    await page.getByRole('button', { name: /^save$/i }).click();

    // AC-110: at least one validation error shown (react-hook-form helperText errors)
    await expect(
      page.locator('p.Mui-error, [role="alert"], .MuiFormHelperText-root.Mui-error').first(),
    ).toBeVisible({ timeout: 5_000 });
  });

  // ---------------------------------------------------------------------------
  // US-027 — Jira Form
  // ---------------------------------------------------------------------------

  test('clicking Connect Jira reveals Jira-specific form fields (AC-111)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    await page.getByRole('tab', { name: /^integration$/i }).click();

    const connectJiraButton = page.getByRole('button', { name: /connect jira/i });
    await expect(connectJiraButton).toBeVisible({ timeout: 10_000 });
    await connectJiraButton.click();

    // AC-111: Jira-specific fields visible
    await expect(page.getByLabel(/base url/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/project key/i)).toBeVisible();
    await expect(page.getByLabel(/"in testing" status name/i)).toBeVisible();
  });

  test('selecting API Token + Email auth reveals Email and Token fields (AC-112)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    await page.getByRole('tab', { name: /^integration$/i }).click();

    const connectJiraButton = page.getByRole('button', { name: /connect jira/i });
    await expect(connectJiraButton).toBeVisible({ timeout: 10_000 });
    await connectJiraButton.click();

    // Auth method is MUI Select — click combobox to open listbox, then select option
    const authMethodCombobox = page.getByRole('combobox', { name: /auth method/i });
    await authMethodCombobox.click();
    await page.getByRole('listbox').waitFor({ state: 'visible' });
    await page.getByRole('option', { name: /api token \+ email/i }).click();

    // AC-112: Email and API Token fields both visible
    await expect(page.getByLabel(/email address/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/^api token$/i)).toBeVisible();
  });

  test('selecting PAT for Jira hides Email field (AC-113)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    await page.getByRole('tab', { name: /^integration$/i }).click();

    const connectJiraButton = page.getByRole('button', { name: /connect jira/i });
    await expect(connectJiraButton).toBeVisible({ timeout: 10_000 });
    await connectJiraButton.click();

    // Switch to PAT auth method via MUI Select
    const authMethodCombobox = page.getByRole('combobox', { name: /auth method/i });
    await authMethodCombobox.click();
    await page.getByRole('listbox').waitFor({ state: 'visible' });
    await page.getByRole('option', { name: /personal access token/i }).click();

    // AC-113: PAT field visible, Email field hidden
    await expect(page.getByLabel(/personal access token/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByLabel(/email address/i)).not.toBeVisible();
  });

  test('submitting empty Jira required fields shows validation errors (AC-114)', async ({ page }) => {
    await page.goto(`/projects/${integrationProjectId}/settings`, { waitUntil: 'load' });
    await page.getByRole('tab', { name: /^integration$/i }).click();

    const connectJiraButton = page.getByRole('button', { name: /connect jira/i });
    await expect(connectJiraButton).toBeVisible({ timeout: 10_000 });
    await connectJiraButton.click();

    // Submit empty form
    await page.getByRole('button', { name: /^save$/i }).click();

    // AC-114: at least one validation error shown
    await expect(
      page.locator('p.Mui-error, [role="alert"], .MuiFormHelperText-root.Mui-error').first(),
    ).toBeVisible({ timeout: 5_000 });
  });
});
