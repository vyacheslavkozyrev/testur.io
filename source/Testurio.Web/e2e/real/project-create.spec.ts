/**
 * Project Creation specs — US-019 (Happy Path), US-020 (Validation Errors)
 *
 * AC-080–AC-087
 */

import { test, expect } from '@playwright/test';

// ---------------------------------------------------------------------------
// US-019 — Project Creation Happy Path
// ---------------------------------------------------------------------------

test.describe('Project Create — Happy Path', () => {
  test('form renders with required fields at /projects/new (AC-080)', async ({ page }) => {
    await page.goto('/projects/new', { waitUntil: 'networkidle' });

    await expect(page.getByLabel(/name/i).or(page.locator('input[name="name"]'))).toBeVisible();
    await expect(page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]'))).toBeVisible();
    await expect(
      page.getByLabel(/testing strategy/i).or(page.locator('textarea[name="testingStrategy"], select[name="testType"]')).first(),
    ).toBeVisible();
  });

  test('creates project, navigates to settings page, project appears in list (AC-081, AC-082, AC-083)', async ({ page, request }) => {
    let createdProjectId: string | null = null;

    try {
      await page.goto('/projects/new', { waitUntil: 'networkidle' });

      // Fill the creation form
      const nameField = page.getByLabel(/name/i).or(page.locator('input[name="name"]')).first();
      const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
      const strategyField = page
        .getByLabel(/testing strategy/i)
        .or(page.locator('textarea[name="testingStrategy"]'))
        .or(page.locator('select[name="testType"]'))
        .first();

      await nameField.fill('[E2E] Created Project');
      await urlField.fill('https://created.example.com');

      const tagName = await strategyField.evaluate((el) => el.tagName.toLowerCase());
      if (tagName === 'select') {
        await strategyField.selectOption({ index: 1 });
      } else {
        await strategyField.fill('E2E automated testing strategy');
      }

      await page.getByRole('button', { name: /create project|save|submit/i }).click();

      // AC-081: navigates to new project's settings page
      await page.waitForURL(/\/projects\/.+\/settings/, { timeout: 15_000 });

      // Extract projectId from the URL
      const match = page.url().match(/\/projects\/([^/]+)\/settings/);
      if (match) createdProjectId = match[1];

      // AC-082: project appears in the project list
      await page.goto('/projects', { waitUntil: 'networkidle' });
      await expect(page.getByText('[E2E] Created Project').first()).toBeVisible({ timeout: 10_000 });
    } finally {
      // AC-083: inline teardown — delete the created project
      if (createdProjectId) {
        const del = await request.delete(`/v1/projects/${createdProjectId}`);
        console.log(`[project-create] Deleted [E2E] Created Project (${createdProjectId}) — status ${del.status()}`);
      }
    }
  });
});

// ---------------------------------------------------------------------------
// US-020 — Project Creation Validation Errors
// ---------------------------------------------------------------------------

test.describe('Project Create — Validation Errors', () => {
  test('empty Name field shows inline validation error, no navigation (AC-084)', async ({ page }) => {
    await page.goto('/projects/new', { waitUntil: 'networkidle' });

    // Fill URL and strategy, leave name empty
    const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
    await urlField.fill('https://example.com');

    const strategyField = page
      .getByLabel(/testing strategy/i)
      .or(page.locator('textarea[name="testingStrategy"]'))
      .or(page.locator('select[name="testType"]'))
      .first();

    const tagName = await strategyField.evaluate((el) => el.tagName.toLowerCase());
    if (tagName === 'select') {
      await strategyField.selectOption({ index: 1 });
    } else {
      await strategyField.fill('Some strategy');
    }

    await page.getByRole('button', { name: /create project|save|submit/i }).click();

    // AC-084: validation error on Name field
    await expect(page.getByText(/name is required|project name is required/i)).toBeVisible({ timeout: 5_000 });
    // Still on /projects/new
    expect(page.url()).toContain('/projects/new');
  });

  test('invalid URL shows inline validation error (AC-085)', async ({ page }) => {
    await page.goto('/projects/new', { waitUntil: 'networkidle' });

    const nameField = page.getByLabel(/name/i).or(page.locator('input[name="name"]')).first();
    await nameField.fill('[E2E] Validation Test');

    const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
    await urlField.fill('not-a-url');

    const strategyField = page
      .getByLabel(/testing strategy/i)
      .or(page.locator('textarea[name="testingStrategy"]'))
      .or(page.locator('select[name="testType"]'))
      .first();

    const tagName = await strategyField.evaluate((el) => el.tagName.toLowerCase());
    if (tagName === 'select') {
      await strategyField.selectOption({ index: 1 });
    } else {
      await strategyField.fill('Some strategy');
    }

    await page.getByRole('button', { name: /create project|save|submit/i }).click();

    // AC-085: validation error on URL field
    await expect(page.getByText(/valid url|invalid url|must be a valid url/i)).toBeVisible({ timeout: 5_000 });
    expect(page.url()).toContain('/projects/new');
  });

  test('empty Testing Strategy shows inline validation error (AC-086)', async ({ page }) => {
    await page.goto('/projects/new', { waitUntil: 'networkidle' });

    const nameField = page.getByLabel(/name/i).or(page.locator('input[name="name"]')).first();
    await nameField.fill('[E2E] Validation Test');

    const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
    await urlField.fill('https://example.com');

    // Leave strategy empty
    await page.getByRole('button', { name: /create project|save|submit/i }).click();

    // AC-086: validation error on Testing Strategy field
    await expect(
      page.getByText(/testing strategy is required|strategy is required/i),
    ).toBeVisible({ timeout: 5_000 });
    expect(page.url()).toContain('/projects/new');
  });

  test('all three validation errors can appear simultaneously (AC-087)', async ({ page }) => {
    await page.goto('/projects/new', { waitUntil: 'networkidle' });

    // Submit with all fields empty / invalid
    const urlField = page.getByLabel(/product url/i).or(page.locator('input[name="productUrl"]')).first();
    await urlField.fill('not-a-url');

    await page.getByRole('button', { name: /create project|save|submit/i }).click();

    // AC-087: all three errors visible simultaneously
    await expect(page.getByText(/name is required|project name is required/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByText(/valid url|invalid url|must be a valid url/i)).toBeVisible();
    await expect(
      page.getByText(/testing strategy is required|strategy is required/i),
    ).toBeVisible();
  });
});
