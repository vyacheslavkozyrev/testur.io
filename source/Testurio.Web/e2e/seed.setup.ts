import { test as setup, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const seedFile = path.join(__dirname, '.auth/seed.json');

setup('create seed project', async ({ request }) => {
  let projectId: string | null = null;

  // 1. Reuse from seed.json if the project is still alive
  if (fs.existsSync(seedFile)) {
    const { projectId: existingId } = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
    const check = await request.get(`/v1/projects/${existingId}`);
    if (check.ok()) {
      projectId = existingId;
    }
  }

  // 2. seed.json missing or stale — scan the project list by name
  if (!projectId) {
    const listRes = await request.get('/v1/projects');
    if (listRes.ok()) {
      const projects = (await listRes.json()) as Array<{ projectId: string; name: string }>;
      const existing = projects.find((p) => p.name === '[E2E] Seed Project');
      if (existing) {
        projectId = existing.projectId;
        console.log('Reusing existing seed project found by name:', projectId);
      }
    }
  }

  // 3. No seed project anywhere — create one
  if (!projectId) {
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
    projectId = (JSON.parse(body) as { projectId: string }).projectId;
  }

  fs.mkdirSync(path.dirname(seedFile), { recursive: true });
  fs.writeFileSync(seedFile, JSON.stringify({ projectId }, null, 2));

  // Ensure ADO integration is configured — required by work-item-filter tests
  const integrationRes = await request.get(`/v1/projects/${projectId}/integrations`);
  const integration = integrationRes.ok()
    ? (await integrationRes.json() as { pmTool: string | null })
    : null;

  if (!integration?.pmTool) {
    const adoRes = await request.post(`/v1/projects/${projectId}/integrations/ado`, {
      data: {
        orgUrl: 'https://dev.azure.com/e2e-test-org',
        projectName: 'E2ETestProject',
        team: 'E2ETestTeam',
        inTestingStatus: 'In Testing',
        authMethod: 'pat',
        pat: 'e2e-placeholder-pat',
      },
    });
    console.log('POST integrations/ado status:', adoRes.status());
  }
});
