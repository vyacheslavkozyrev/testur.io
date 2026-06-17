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

// __dirname is e2e/real/ — four levels up reaches the repo root where .env.test lives
dotenv.config({ path: path.resolve(__dirname, '../../../../.env.test') });

const authFile = path.join(__dirname, '../.auth/user.json');

export default async function globalTeardown(): Promise<void> {
  if (!process.env.BASE_URL) {
    console.log('[teardown] BASE_URL not set — not a dev run, skipping cleanup.');
    return;
  }

  if (!fs.existsSync(authFile)) {
    console.log('[teardown] No auth state found — skipping cleanup.');
    return;
  }

  const baseURL = process.env.BASE_URL;

  const apiContext = await createRequest.newContext({
    baseURL,
    storageState: authFile,
    extraHTTPHeaders: {
      'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
      'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
    },
  });

  try {
    // Sweep for per-test [E2E] projects created during the run.
    // The seed project ("[E2E] Seed Project") is intentionally kept alive so the
    // next run can reuse it without hitting the trial project limit.
    const listRes = await apiContext.get('/v1/projects');
    if (!listRes.ok()) {
      console.warn(`[teardown] GET /v1/projects returned ${listRes.status()} — cannot sweep [E2E] projects.`);
    } else {
      const projects = (await listRes.json()) as Array<{ projectId: string; name: string }>;
      const toDelete = projects.filter(
        (p) => p.name?.startsWith('[E2E]') && p.name !== '[E2E] Seed Project',
      );
      console.log(`[teardown] Found ${toDelete.length} per-test [E2E] project(s) to delete.`);

      for (const project of toDelete) {
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
  }
}
