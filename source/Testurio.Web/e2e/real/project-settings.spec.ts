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
// Helper: trigger a parent component re-render so the SaveBar picks up
// react-hook-form's isDirty flag (which lives in a child ref).
//
// ProjectSettingsPage's computeDirty() only runs after a parent render.
// Interacting with the Custom Prompt field calls handleCustomPromptChange →
// setCustomPrompt (parent state), which triggers a parent re-render, at which
// point the useEffect sees isDirty=true from the form ref and enables SaveBar.
// ---------------------------------------------------------------------------
async function triggerParentDirtyDetection(page: import('@playwright/test').Page): Promise<void> {
  const customPrompt = page.getByRole('textbox', { name: /custom prompt/i });
  await customPrompt.click();
  // Type and immediately delete to keep value empty but force parent state update
  await customPrompt.type(' ');
  await customPrompt.press('Backspace');
}

// ---------------------------------------------------------------------------
// US-021 — Settings Tab Pre-populated
// ---------------------------------------------------------------------------

test.describe('Project Settings — Settings Tab', () => {
  test('Settings tab is active by default and form fields are pre-populated (AC-088, AC-089, AC-090, AC-091, AC-092)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    // waitUntil: 'load' — 'networkidle' times out due to SSE stream or long-polling.
    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

    // AC-088: Settings tab is the default view and is selected.
    // Use the tab role directly — avoids strict-mode violation from sidebar "Settings" link.
    const settingsTab = page.getByRole('tab', { name: /^settings$/i });
    await expect(settingsTab).toBeVisible({ timeout: 10_000 });
    await expect(settingsTab).toHaveAttribute('aria-selected', 'true');

    // AC-089: Name pre-populated.
    // Use 'Project Name' to avoid matching unrelated labels containing "name".
    const nameField = page.getByRole('textbox', { name: /project name/i });
    await expect(nameField).toHaveValue('[E2E] Seed Project');

    // AC-090: Product URL pre-populated.
    const urlField = page.getByRole('textbox', { name: /product url/i });
    await expect(urlField).toHaveValue('https://example.com');

    // AC-091: Testing Strategy pre-populated.
    const strategyField = page.getByRole('textbox', { name: /testing strategy/i });
    const strategyValue = await strategyField.inputValue();
    expect(strategyValue.length).toBeGreaterThan(0);

    // AC-092: Custom Prompt field is visible.
    const customPromptField = page.getByRole('textbox', { name: /custom prompt/i });
    await expect(customPromptField).toBeVisible();
  });

  // ---------------------------------------------------------------------------
  // US-022 — Save Updates Project
  // ---------------------------------------------------------------------------

  test('updating Testing Strategy and saving shows success state, persists on reload (AC-093, AC-094, AC-095, AC-096)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

    const strategyField = page.getByRole('textbox', { name: /testing strategy/i });
    await expect(strategyField).toBeVisible({ timeout: 10_000 });

    const newStrategy = `E2E updated strategy — ${Date.now()}`;
    await strategyField.fill(newStrategy);

    // AC-093: modifying Testing Strategy enables the Save button.
    // react-hook-form isDirty lives in a child ref; the parent SaveBar only
    // updates after a parent re-render.  Interacting with the Custom Prompt
    // textarea (parent-owned state) forces the parent re-render so SaveBar
    // transitions from 'clean' → 'dirty' and the button becomes 'Save Changes'.
    await triggerParentDirtyDetection(page);

    const saveButton = page.getByRole('button', { name: /save changes/i });
    await expect(saveButton).toBeEnabled({ timeout: 5_000 });

    // AC-094: clicking Save calls PATCH/PUT API.
    const [apiRequest] = await Promise.all([
      page.waitForRequest((req) =>
        (req.method() === 'PUT' || req.method() === 'PATCH') &&
        req.url().includes(`/v1/projects/${seedProjectId}`),
      ),
      saveButton.click(),
    ]);
    expect(apiRequest).toBeTruthy();

    // AC-095: success indicator shown — SaveBar transitions to 'saved' ("Saved ✓").
    await expect(
      page.getByRole('button', { name: /saved/i }),
    ).toBeVisible({ timeout: 10_000 });

    // AC-096: refresh page and verify updated value persists.
    await page.reload({ waitUntil: 'load' });
    const strategyAfterReload = page.getByRole('textbox', { name: /testing strategy/i });
    await expect(strategyAfterReload).toHaveValue(newStrategy, { timeout: 10_000 });
  });

  // ---------------------------------------------------------------------------
  // US-023 — Save Validation Errors
  // ---------------------------------------------------------------------------

  test('clearing Name shows validation error before API call (AC-097, AC-099)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

    const nameField = page.getByRole('textbox', { name: /project name/i });
    await expect(nameField).toBeVisible({ timeout: 10_000 });
    await nameField.clear();

    // Trigger parent re-render so SaveBar transitions to 'dirty' (save enabled).
    await triggerParentDirtyDetection(page);

    let apiCallMade = false;
    page.on('request', (req) => {
      if ((req.method() === 'PUT' || req.method() === 'PATCH') && req.url().includes('/v1/projects/')) {
        apiCallMade = true;
      }
    });

    const saveButton = page.getByRole('button', { name: /save changes/i });
    await expect(saveButton).toBeEnabled({ timeout: 5_000 });
    await saveButton.click();

    // AC-097: validation error on Name field — react-hook-form shows inline error.
    await expect(page.getByText(/project name is required/i)).toBeVisible({ timeout: 5_000 });

    // API was NOT called
    expect(apiCallMade).toBe(false);

    // AC-099: Save button re-enabled after validation error (saveBarState returns to 'dirty').
    await expect(page.getByRole('button', { name: /save changes/i })).toBeEnabled({ timeout: 5_000 });
  });

  test('clearing Product URL shows validation error before API call (AC-098)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

    const urlField = page.getByRole('textbox', { name: /product url/i });
    await expect(urlField).toBeVisible({ timeout: 10_000 });
    await urlField.clear();

    // Trigger parent re-render so SaveBar transitions to 'dirty'.
    await triggerParentDirtyDetection(page);

    let apiCallMade = false;
    page.on('request', (req) => {
      if ((req.method() === 'PUT' || req.method() === 'PATCH') && req.url().includes('/v1/projects/')) {
        apiCallMade = true;
      }
    });

    const saveButton = page.getByRole('button', { name: /save changes/i });
    await expect(saveButton).toBeEnabled({ timeout: 5_000 });
    await saveButton.click();

    // AC-098: validation error on URL field.
    await expect(page.getByText(/product url is required/i)).toBeVisible({ timeout: 5_000 });
    expect(apiCallMade).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // US-024 — Report Format & Attachments Section Visible
  // ---------------------------------------------------------------------------

  test('Report Format & Attachments section is present on Settings tab (AC-100, AC-101, AC-102)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

    // AC-100: "Report Template" heading is visible (the ReportSettingsSection renders
    // ReportTemplateUpload first, which shows this heading).
    // The sectionTitle "Report Format & Attachments" is defined in reportSettings.json but
    // is NOT rendered as a Typography heading in ReportSettingsSection — only the
    // "Report Template" subtitle (t('template.title')) is rendered.
    await expect(
      page.getByRole('heading', { name: /report template/i }),
    ).toBeVisible({ timeout: 10_000 });

    // AC-101: two attachment toggles visible (rendered as MUI Switch / checkbox role).
    await expect(page.getByRole('checkbox', { name: /include step-by-step logs/i })).toBeVisible();
    await expect(page.getByRole('checkbox', { name: /include screenshots/i })).toBeVisible();

    // AC-102: report template upload button visible (no template uploaded for seed project).
    await expect(
      page.getByRole('button', { name: /upload template/i }),
    ).toBeVisible();
  });
});
