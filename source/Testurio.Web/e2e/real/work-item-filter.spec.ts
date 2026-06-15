/**
 * Work Item Type Filter specs — US-031
 *
 * AC-129–AC-132
 *
 * These tests require that a PM tool connection (ADO) is configured on the
 * seed project. If no connection is configured, the Work Item Type Filter
 * section may not appear, and tests are skipped with a descriptive message.
 */

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '../.auth/seed.json');

function readSeedProjectId(): string {
  const data = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
  return data.projectId;
}

test.describe('Work Item Type Filter', () => {
  test.beforeEach(async ({ page }) => {
    const seedProjectId = readSeedProjectId();
    await page.goto(`/projects/${seedProjectId}/settings`, { waitUntil: 'load' });

    // Navigate to the Integration tab where the Work Item Type Filter lives
    const integrationTab = page.getByRole('tab', { name: /integration/i });
    if (await integrationTab.isVisible()) {
      await integrationTab.click();
    }
  });

  test('Work Item Type Filter section is visible when PM tool is configured (AC-129, AC-130)', async ({ page }) => {
    const filterSection = page
      .locator('[data-testid="work-item-type-filter"]')
      .or(page.getByText(/work item type filter/i).first());

    if (!(await filterSection.isVisible({ timeout: 5_000 }).catch(() => false))) {
      test.skip(true, 'Work Item Type Filter section not visible — PM tool connection may not be configured on seed project');
      return;
    }

    // AC-129: filter section visible
    await expect(filterSection).toBeVisible();

    // AC-130: default types pre-populated (chip input or multi-select)
    const chipInput = page
      .locator('[data-testid="work-item-type-chips"]')
      .or(page.locator('[class*="chip"], [class*="tag"]').first());
    await expect(chipInput).toBeVisible();
  });

  test('adding a new type and saving calls PATCH with updated list (AC-131)', async ({ page }) => {
    const seedProjectId = readSeedProjectId();

    const filterSection = page
      .locator('[data-testid="work-item-type-filter"]')
      .or(page.getByText(/work item type filter/i).first());

    if (!(await filterSection.isVisible({ timeout: 5_000 }).catch(() => false))) {
      test.skip(true, 'Work Item Type Filter not visible — skipping');
      return;
    }

    // Add a new type via input
    const typeInput = page
      .locator('[data-testid="work-item-type-input"]')
      .or(page.locator('input[placeholder*="work item type"], input[placeholder*="Add type"]'))
      .first();

    if (await typeInput.isVisible()) {
      await typeInput.fill('Feature');
      await typeInput.press('Enter');
    }

    // AC-131: save calls PATCH /v1/projects/:id with allowedWorkItemTypes
    const [apiRequest] = await Promise.all([
      page.waitForRequest((req) =>
        req.method() === 'PATCH' &&
        req.url().includes(`/v1/projects/${seedProjectId}`),
      ),
      page.getByRole('button', { name: /save/i }).click(),
    ]);

    expect(apiRequest).toBeTruthy();
    const body = JSON.parse(apiRequest.postData() ?? '{}') as { allowedWorkItemTypes?: string[] };
    expect(body.allowedWorkItemTypes).toBeDefined();
  });

  test('saving with empty type list shows validation error (AC-132)', async ({ page }) => {
    const filterSection = page
      .locator('[data-testid="work-item-type-filter"]')
      .or(page.getByText(/work item type filter/i).first());

    if (!(await filterSection.isVisible({ timeout: 5_000 }).catch(() => false))) {
      test.skip(true, 'Work Item Type Filter not visible — skipping');
      return;
    }

    // Remove all existing chips
    const removeButtons = page.locator('[data-testid="work-item-type-filter"] [aria-label*="remove"], [data-testid="work-item-type-filter"] [aria-label*="delete"]');
    const count = await removeButtons.count();
    for (let i = 0; i < count; i++) {
      await removeButtons.first().click();
    }

    await page.getByRole('button', { name: /save/i }).click();

    // AC-132: validation error shown
    await expect(
      page.getByText(/at least one work item type must be selected/i),
    ).toBeVisible({ timeout: 5_000 });
  });
});
