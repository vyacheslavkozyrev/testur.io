import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';
import * as path from 'path';

dotenv.config({ path: path.resolve(__dirname, '../../.env.test') });

const localBaseURL = process.env.BASE_URL_LOCAL ?? 'http://localhost:3000';
const devBaseURL   = process.env.BASE_URL       ?? 'http://localhost:3000';
// const devBaseURL   = process.env.BASE_URL       ?? 'https://web-dev01.testur.io';

const localAuthFile = path.join(__dirname, 'e2e/.auth/local.json');
const devAuthFile   = path.join(__dirname, 'e2e/.auth/user.json');

const devUse = {
  baseURL: devBaseURL,
  extraHTTPHeaders: {
    'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID     ?? '',
    'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
  },
};

const chromiumLaunch = {
  executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ?? undefined,
  args: ['--no-sandbox', '--disable-dev-shm-usage', '--disable-gpu'],
};

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: 'html',
  // Teardown cleans up [E2E] projects from the real API after dev runs.
  // Safe to register unconditionally — teardown.ts no-ops when BASE_URL is not set.
  globalTeardown: path.join(__dirname, 'e2e/real/teardown.ts'),

  use: {
    trace: 'on-first-retry',
    navigationTimeout: 60_000,
  },

  projects: [
    // ── local ────────────────────────────────────────────────────────────────
    // Mock-based specs + merged specs (real-API tests skip via project.name guard).
    // Next.js is started automatically via webServer (production build).
    // local:setup creates a synthetic server-side session so the auth guard passes.
    {
      name: 'local:setup',
      testMatch: /local\.setup\.ts/,
      use: { baseURL: localBaseURL },
    },
    {
      name: 'local',
      testMatch: /^(?!.*[/\\]real[/\\]).+\.spec\.ts$/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: localBaseURL,
        storageState: localAuthFile,
        launchOptions: chromiumLaunch,
      },
      dependencies: ['local:setup'],
    },

    // ── dev ──────────────────────────────────────────────────────────────────
    // Real authenticated specs against the deployed dev environment.
    // Runs ALL specs: e2e/ (merged) + e2e/real/ (integration-only).
    // Run with:  yarn test:e2e:dev
    {
      name: 'dev:auth',
      testMatch: /auth\.setup\.ts/,
      use: devUse,
    },
    {
      name: 'dev:seed',
      testMatch: /seed\.setup\.ts/,
      use: { ...devUse, storageState: devAuthFile },
      dependencies: ['dev:auth'],
    },
    {
      name: 'dev',
      testMatch: /\.spec\.ts$/,
      use: {
        ...devices['Desktop Chrome'],
        ...devUse,
        storageState: devAuthFile,
        launchOptions: chromiumLaunch,
      },
      dependencies: ['dev:auth', 'dev:seed'],
    },
  ],

  // Only spin up the local Next.js server when the local project is selected.
  // Uses the production build (next build + next start) to avoid the memory
  // overhead of next dev. If a server is already on the port it is reused.
  // For the dev project the server is assumed to be already running at BASE_URL.
  ...(process.argv.some(a => a === 'local' || a === '--project=local') && {
    webServer: {
      command: 'yarn build && yarn start',
      url: `${localBaseURL}/projects`,
      reuseExistingServer: true,
      timeout: 300_000,
    },
  }),
});
