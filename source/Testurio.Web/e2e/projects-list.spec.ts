import { test, expect } from '@playwright/test';

/**
 * E2E tests for feature 0010b — Projects List Page
 *
 * Prerequisites:
 * - The dev server is running and authentication is handled by the test setup
 *   (e.g. storageState with a pre-authenticated session).
 * - The MSW mock server (or a real API) returns seeded project data.
 *
 * Seed data expected by these tests (provided by global setup / fixtures):
 *   - Project A: id='aaaaaaaa-0000-0000-0000-000000000001', createdAt='2026-05-10T00:00:00Z'
 *   - Project B: id='bbbbbbbb-0000-0000-0000-000000000002', createdAt='2026-05-01T00:00:00Z'
 */

test.describe('Projects List Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/projects');
  });

  test('AC-003: card grid is sorted by createdAt descending — newest project appears first', async ({
    page,
  }) => {
    const cards = page.locator('[data-testid="project-card"], .MuiCard-root');
    await expect(cards.first()).toContainText('Project A');
    await expect(cards.nth(1)).toContainText('Project B');
  });

  test('AC-009/AC-010: empty state CTA navigates to /projects/new', async ({
    page,
  }) => {
    // Navigate to the page when no projects exist (handled by fixture/mock override)
    await page.goto('/projects?seed=empty');
    await expect(page.getByText('No projects yet')).toBeVisible();

    await page.getByRole('button', { name: 'Create your first project' }).click();
    await expect(page).toHaveURL('/projects/new');
  });

  test('AC-013: clicking a project card navigates to /projects/:id/history', async ({
    page,
  }) => {
    const projectId = 'aaaaaaaa-0000-0000-0000-000000000001';
    // Click the card action area (the link wrapping the card content)
    const cardLink = page.locator(`a[href="/projects/${projectId}/history"]`).first();
    await cardLink.click();
    await expect(page).toHaveURL(`/projects/${projectId}/history`);
  });

  test('AC-016/AC-018: clicking the edit icon navigates to /projects/:id/settings without opening history', async ({
    page,
  }) => {
    const projectId = 'aaaaaaaa-0000-0000-0000-000000000001';

    const editButton = page
      .locator('.MuiCard-root')
      .first()
      .getByRole('button', { name: 'Edit project' });

    await editButton.click();

    await expect(page).toHaveURL(`/projects/${projectId}/settings`);
    // Ensure the history page was NOT navigated to
    await expect(page).not.toHaveURL(`/projects/${projectId}/history`);
  });
});
