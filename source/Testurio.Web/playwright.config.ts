import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';
import * as path from 'path';

dotenv.config({ path: path.resolve(__dirname, '../../.env.test') });

const localBaseURL = process.env.BASE_URL_LOCAL ?? 'http://localhost:3100';
const devBaseURL   = process.env.BASE_URL       ?? 'http://localhost:3000';

const authFile = path.join(__dirname, 'e2e/.auth/user.json');

const devUse = {
  baseURL: devBaseURL,
  extraHTTPHeaders: {
    'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID     ?? '',
    'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
  },
};

const chromiumLaunch = {
  executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ?? undefined,
};

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: 'html',
  globalTeardown: path.join(__dirname, 'e2e/real/teardown.ts'),

  use: {
    trace: 'on-first-retry',
    navigationTimeout: 60_000,
  },

  projects: [
    // ── local ────────────────────────────────────────────────────────────────
    // Mock-based specs only. Next.js is started automatically via webServer.
    {
      name: 'local',
      testMatch: /(?<!real\/).+\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: localBaseURL,
        launchOptions: chromiumLaunch,
      },
    },

    // ── dev ──────────────────────────────────────────────────────────────────
    // Real authenticated specs against the deployed dev environment.
    // Run with:  yarn test:e2e:dev
    {
      name: 'dev:auth',
      testMatch: /auth\.setup\.ts/,
      use: devUse,
    },
    {
      name: 'dev:seed',
      testMatch: /seed\.setup\.ts/,
      use: { ...devUse, storageState: authFile },
      dependencies: ['dev:auth'],
    },
    {
      name: 'dev',
      testMatch: /real\/.+\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        ...devUse,
        storageState: authFile,
        launchOptions: chromiumLaunch,
      },
      dependencies: ['dev:auth', 'dev:seed'],
    },
  ],

  webServer: {
    command: 'npm run dev -- --port 3100',
    url: `${localBaseURL}/projects`,
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
