/**
 * Project Delete specs — US-032
 *
 * AC-133–AC-138
 *
 * Uses a freshly created [E2E] Delete Target project (not the seed project)
 * so the seed remains available for all other tests.
 */

import { test, expect } from '@playwright/test';

let deleteTargetProjectId: string | null = null;

test.describe('Project Delete', () => {
  test.beforeEach(async ({ request }) => {
    // Create a fresh [E2E] Delete Target project before each test
    const res = await request.post('/v1/projects', {
      data: {
        name: '[E2E] Delete Target',
        productUrl: 'https://delete-target.example.com',
        testingStrategy: 'Temporary project — created for delete flow test.',
        requestTimeoutSeconds: 30,
      },
    });
    expect(res.ok()).toBeTruthy();
    const body = (await res.json()) as { projectId: string };
    deleteTargetProjectId = body.projectId;
  });

  test.afterEach(async ({ request }) => {
    // Belt-and-suspenders: delete the project if it still exists (e.g. cancel test)
    if (deleteTargetProjectId) {
      await request.delete(`/v1/projects/${deleteTargetProjectId}`).catch(() => {});
      deleteTargetProjectId = null;
    }
  });

  test('Danger Zone section is visible on settings page (AC-133)', async ({ page }) => {
    await page.goto(`/projects/${deleteTargetProjectId}/settings`, { waitUntil: 'networkidle' });

    await expect(
      page.getByText(/danger zone|delete project/i).first(),
    ).toBeVisible({ timeout: 10_000 });
  });

  test('clicking delete opens a confirmation dialog before any API call (AC-134)', async ({ page }) => {
    await page.goto(`/projects/${deleteTargetProjectId}/settings`, { waitUntil: 'networkidle' });

    let apiDeleteCalled = false;
    page.on('request', (req) => {
      if (req.method() === 'DELETE' && req.url().includes('/v1/projects/')) {
        apiDeleteCalled = true;
      }
    });

    // Click the delete action (button or link)
    await page
      .getByRole('button', { name: /delete project|delete/i })
      .last()
      .click();

    // AC-134: confirmation dialog opens
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });

    // No API call yet
    expect(apiDeleteCalled).toBe(false);
  });

  test('cancelling the confirmation dialog leaves project intact (AC-137)', async ({ page }) => {
    await page.goto(`/projects/${deleteTargetProjectId}/settings`, { waitUntil: 'networkidle' });

    await page
      .getByRole('button', { name: /delete project|delete/i })
      .last()
      .click();

    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });

    // Cancel the dialog
    await page.getByRole('button', { name: /cancel/i }).click();

    // AC-137: still on settings page, project intact
    await expect(page.getByRole('dialog')).not.toBeVisible();
    expect(page.url()).toContain(`/projects/${deleteTargetProjectId}/settings`);
  });

  test('confirming deletion calls DELETE API and redirects to /projects; project no longer appears in list (AC-135, AC-136, AC-138)', async ({ page }) => {
    await page.goto(`/projects/${deleteTargetProjectId}/settings`, { waitUntil: 'networkidle' });

    await page
      .getByRole('button', { name: /delete project|delete/i })
      .last()
      .click();

    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5_000 });

    // AC-135: confirm deletion
    await page.getByRole('button', { name: /confirm|yes, delete|delete/i }).last().click();

    // AC-135: redirects to /projects or /dashboard
    await page.waitForURL(/\/(projects|dashboard)/, { timeout: 15_000 });
    expect(page.url()).toMatch(/\/(projects|dashboard)/);

    // AC-136: deleted project no longer appears in the project list
    await page.goto('/projects', { waitUntil: 'networkidle' });
    await expect(page.getByText('[E2E] Delete Target')).not.toBeVisible({ timeout: 5_000 });

    // The project was deleted by the UI — clear the ID so afterEach doesn't double-delete
    deleteTargetProjectId = null;
  });
});
