/**
 * Global teardown — runs after all real E2E tests complete.
 *
 * Deletes every project whose name starts with "[E2E]" via the REST API,
 * using the persisted auth session from e2e/.auth/user.json.
 *
 * Explicitly does NOT touch:
 * - Azure AD B2C user accounts
 * - Cosmos DB `Users` documents
 * - `UserSubscriptions` documents
 */

import { request as createRequest } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import * as dotenv from 'dotenv';

dotenv.config({ path: path.resolve(__dirname, '../../../.env.test') });

const authFile = path.join(__dirname, '../.auth/user.json');
const seedFile = path.join(__dirname, '../.auth/seed.json');

export default async function globalTeardown(): Promise<void> {
  if (!fs.existsSync(authFile)) {
    console.log('[teardown] No auth state found — skipping cleanup.');
    return;
  }

  const baseURL = process.env.BASE_URL!;

  const apiContext = await createRequest.newContext({
    baseURL,
    storageState: authFile,
    extraHTTPHeaders: {
      'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
      'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
    },
  });

  try {
    // 1. Delete the seed project by explicit ID (fast path)
    if (fs.existsSync(seedFile)) {
      try {
        const { projectId } = JSON.parse(fs.readFileSync(seedFile, 'utf-8')) as { projectId: string };
        const res = await apiContext.delete(`/v1/projects/${projectId}`);
        if (res.status() === 404) {
          console.log(`[teardown] Seed project ${projectId} already gone (404) — skipping.`);
        } else {
          console.log(`[teardown] Deleted seed project ${projectId} — status ${res.status()}`);
        }
      } catch (err) {
        console.warn('[teardown] Could not delete seed project:', err);
      }
    }

    // 2. Sweep for any remaining [E2E] projects created during the run
    const listRes = await apiContext.get('/v1/projects');
    if (!listRes.ok()) {
      console.warn(`[teardown] GET /v1/projects returned ${listRes.status()} — cannot sweep remaining [E2E] projects.`);
    } else {
      const projects = (await listRes.json()) as Array<{ projectId: string; name: string }>;
      const e2eProjects = projects.filter((p) => p.name?.startsWith('[E2E]'));
      console.log(`[teardown] Found ${e2eProjects.length} remaining [E2E] project(s) to delete.`);

      for (const project of e2eProjects) {
        try {
          const del = await apiContext.delete(`/v1/projects/${project.projectId}`);
          if (del.status() === 404) {
            console.log(`[teardown] Project "${project.name}" (${project.projectId}) already gone — skipping.`);
          } else {
            console.log(`[teardown] Deleted project "${project.name}" (${project.projectId}) — status ${del.status()}`);
          }
        } catch (err) {
          console.warn(`[teardown] Failed to delete project "${project.name}":`, err);
        }
      }
    }
  } finally {
    await apiContext.dispose();

    // 3. Remove seed.json so the next run starts fresh
    if (fs.existsSync(seedFile)) {
      fs.unlinkSync(seedFile);
      console.log('[teardown] Removed e2e/.auth/seed.json.');
    }
  }
}
