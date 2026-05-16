import { test, expect } from '@playwright/test';
import type { ProjectDto } from '../src/types/project.types';

const PROJECT_A: ProjectDto = {
  projectId: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'Project A',
  productUrl: 'https://project-a.example.com',
  testingStrategy: 'API and UI testing for Project A',
  customPrompt: null,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
};

const PROJECT_B: ProjectDto = {
  projectId: 'bbbbbbbb-0000-0000-0000-000000000002',
  name: 'Project B',
  productUrl: 'https://project-b.example.com',
  testingStrategy: 'UI testing for Project B',
  customPrompt: null,
  createdAt: '2026-05-01T00:00:00Z',
  updatedAt: '2026-05-01T00:00:00Z',
};

const MOCK_USER = {
  id: '00000000-0000-0000-0000-000000000099',
  displayName: 'Jane Smith',
  email: 'jane.smith@example.com',
};

test.describe('Projects List Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('/api/auth/me', (route) =>
      route.fulfill({ json: MOCK_USER }),
    );
  });

  test('AC-003: card grid is sorted by createdAt descending — newest project appears first', async ({
    page,
  }) => {
    // API returns B first (older), but the UI sorts by createdAt desc → A appears first
    await page.route('/v1/projects', (route) =>
      route.fulfill({ json: [PROJECT_B, PROJECT_A] }),
    );

    await page.goto('/projects');

    const cards = page.locator('.MuiCard-root');
    await expect(cards.first()).toContainText('Project A');
    await expect(cards.nth(1)).toContainText('Project B');
  });

  test('AC-009/AC-010: empty state CTA navigates to /projects/new', async ({
    page,
  }) => {
    await page.route('/v1/projects', (route) =>
      route.fulfill({ json: [] }),
    );

    await page.goto('/projects');

    await expect(page.getByText('No projects yet')).toBeVisible();
    await page.getByRole('button', { name: 'Create your first project' }).click();
    await expect(page).toHaveURL('/projects/new');
  });

  test('AC-013: clicking a project card navigates to /projects/:id/history', async ({
    page,
  }) => {
    await page.route('/v1/projects', (route) =>
      route.fulfill({ json: [PROJECT_A, PROJECT_B] }),
    );

    await page.goto('/projects');

    const projectId = PROJECT_A.projectId;
    const cardLink = page.locator(`a[href="/projects/${projectId}/history"]`).first();
    await cardLink.click();
    await expect(page).toHaveURL(`/projects/${projectId}/history`);
  });

  test('AC-016/AC-018: clicking the edit icon navigates to /projects/:id/settings without opening history', async ({
    page,
  }) => {
    await page.route('/v1/projects', (route) =>
      route.fulfill({ json: [PROJECT_A, PROJECT_B] }),
    );

    await page.goto('/projects');

    const projectId = PROJECT_A.projectId;
    const editButton = page
      .locator('.MuiCard-root')
      .first()
      .getByRole('button', { name: 'Edit project' });

    await editButton.click();

    await expect(page).toHaveURL(`/projects/${projectId}/settings`);
    await expect(page).not.toHaveURL(`/projects/${projectId}/history`);
  });
});
