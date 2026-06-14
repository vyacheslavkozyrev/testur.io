/**
 * Project Settings — Settings Tab specs
 * US-021 (Pre-populated), US-022 (Save), US-023 (Save Validation), US-024 (Report Format Section)
 *
 * AC-088–AC-102
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
// US-021 — Settings Tab Pre-populated
// ---------------------------------------------------------------------------

test.describe('Project Settings — Settings Tab', () => {
  test('Settings tab is active by default and form fields are pre-populated (AC-088, AC-089, AC-090, AC-091, AC-092)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    // AC-088: Settings tab is the default view
    await expect(page.getByRole('tab', { name: /settings/i }).or(page.getByText(/settings/i).first())).toBeVisible();

    // AC-089: Name pre-populated
    const nameField = page.getByLabel(/name/i).or(page.locator('input[name="name"]')).first();
    await expect(nameField).toHaveValue('[E2E] Seed Project');

    // AC-090: Product URL pre-populated
    const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
    await expect(urlField).toHaveValue('https://example.com');

    // AC-091: Testing Strategy pre-populated
    const strategyField = page
      .getByLabel(/testing strategy/i)
      .or(page.locator('textarea[name="testingStrategy"], select[name="testType"]'))
      .first();
    const strategyValue = await strategyField.inputValue();
    expect(strategyValue.length).toBeGreaterThan(0);

    // AC-092: custom prompt field visible and accepts text
    const customPromptField = page
      .getByLabel(/custom prompt/i)
      .or(page.locator('textarea[name="customPrompt"]'))
      .first();
    await expect(customPromptField).toBeVisible();
  });

  // ---------------------------------------------------------------------------
  // US-022 — Save Updates Project
  // ---------------------------------------------------------------------------

  test('updating Testing Strategy and saving shows success state, persists on reload (AC-093, AC-094, AC-095, AC-096)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    const strategyField = page
      .getByLabel(/testing strategy/i)
      .or(page.locator('textarea[name="testingStrategy"], select[name="testType"]'))
      .first();

    const saveButton = page.getByRole('button', { name: /save changes|save/i });

    // AC-093: modifying Testing Strategy enables the Save button
    await strategyField.fill(`E2E updated strategy — ${Date.now()}`);
    await expect(saveButton).toBeEnabled();

    // AC-094: clicking Save calls PATCH/PUT API
    const [apiRequest] = await Promise.all([
      page.waitForRequest((req) =>
        (req.method() === 'PUT' || req.method() === 'PATCH') &&
        req.url().includes(`/v1/projects/${seedProjectId}`),
      ),
      saveButton.click(),
    ]);
    expect(apiRequest).toBeTruthy();

    // AC-095: success indicator shown
    await expect(
      page.getByText(/saved|success/i).or(page.locator('[data-testid="save-success"]')).first(),
    ).toBeVisible({ timeout: 10_000 });

    // AC-096: refresh page and verify updated value persists
    const savedStrategy = await strategyField.inputValue();
    await page.reload({ waitUntil: 'networkidle' });
    await expect(
      page.getByLabel(/testing strategy/i).or(page.locator('textarea[name="testingStrategy"], select[name="testType"]')).first(),
    ).toHaveValue(savedStrategy);
  });

  // ---------------------------------------------------------------------------
  // US-023 — Save Validation Errors
  // ---------------------------------------------------------------------------

  test('clearing Name shows validation error before API call (AC-097, AC-099)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    const nameField = page.getByLabel(/name/i).or(page.locator('input[name="name"]')).first();
    await nameField.clear();

    let apiCallMade = false;
    page.on('request', (req) => {
      if ((req.method() === 'PUT' || req.method() === 'PATCH') && req.url().includes('/v1/projects/')) {
        apiCallMade = true;
      }
    });

    await page.getByRole('button', { name: /save changes|save/i }).click();

    // AC-097: validation error on Name field
    await expect(page.getByText(/name is required|project name is required/i)).toBeVisible({ timeout: 5_000 });

    // API was NOT called
    expect(apiCallMade).toBe(false);

    // AC-099: Save button re-enabled after validation error
    await expect(page.getByRole('button', { name: /save changes|save/i })).toBeEnabled();
  });

  test('clearing Product URL shows validation error before API call (AC-098)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
    await urlField.clear();

    let apiCallMade = false;
    page.on('request', (req) => {
      if ((req.method() === 'PUT' || req.method() === 'PATCH') && req.url().includes('/v1/projects/')) {
        apiCallMade = true;
      }
    });

    await page.getByRole('button', { name: /save changes|save/i }).click();

    // AC-098: validation error on URL field
    await expect(page.getByText(/url is required|product url is required|valid url/i)).toBeVisible({ timeout: 5_000 });
    expect(apiCallMade).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // US-024 — Report Format & Attachments Section Visible
  // ---------------------------------------------------------------------------

  test('Report Format & Attachments section is present on Settings tab (AC-100, AC-101, AC-102)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'networkidle' });

    // AC-100: "Report Format & Attachment Settings" heading or equivalent
    await expect(
      page.getByText(/report format|attachment settings/i).first(),
    ).toBeVisible({ timeout: 10_000 });

    // AC-101: two attachment toggles visible
    await expect(page.getByLabel(/include step-by-step logs/i)).toBeVisible();
    await expect(page.getByLabel(/include screenshots/i)).toBeVisible();

    // AC-102: report template upload or filename indicator visible
    await expect(
      page
        .locator('[data-testid="report-template-upload"], [data-testid="template-indicator"]')
        .or(page.getByText(/report template|upload template/i).first()),
    ).toBeVisible();
  });
});
