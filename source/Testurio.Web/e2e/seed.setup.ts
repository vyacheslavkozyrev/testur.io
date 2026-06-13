import { test as setup, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '.auth/seed.json');

setup('create seed project', async ({ request }) => {
  // Reuse an existing seed project if it is still alive
  if (fs.existsSync(seedFile)) {
    const { projectId } = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
    const check = await request.get(`/v1/projects/${projectId}`);
    if (check.ok()) return;
  }

  const response = await request.post('/v1/projects', {
    data: {
      name: '[E2E] Seed Project',
      productUrl: 'https://example.com',
      testingStrategy: 'Automated E2E seed project — do not delete manually.',
      requestTimeoutSeconds: 30,
    },
  });

  const body = await response.text();
  console.log('POST /v1/projects status:', response.status());
  console.log('POST /v1/projects body:', body);
  expect(response.ok()).toBeTruthy();
  const project = JSON.parse(body) as { projectId: string };

  fs.writeFileSync(seedFile, JSON.stringify({ projectId: project.projectId }, null, 2));
});
