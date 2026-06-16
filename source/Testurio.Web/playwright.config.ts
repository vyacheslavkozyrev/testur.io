import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';
import * as path from 'path';

const env = (process.env.E2E_ENV ?? 'local') as 'local' | 'dev';

if (env === 'dev') {
  dotenv.config({ path: path.resolve(__dirname, '../../.env.test') });
}

const baseURL =
  env === 'dev'
    ? process.env.BASE_URL!
    : (process.env.BASE_URL ?? 'http://localhost:3100');

const authFile = path.join(__dirname, 'e2e/.auth/user.json');

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? (env === 'dev' ? 1 : 2) : 0,
  workers: 1,
  reporter: 'html',

  ...(env === 'dev' && {
    globalTeardown: path.join(__dirname, 'e2e/real/teardown.ts'),
  }),

  use: {
    baseURL,
    trace: 'on-first-retry',
    navigationTimeout: 60_000,
    ...(env === 'dev' && {
      extraHTTPHeaders: {
        'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
        'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
      },
    }),
  },

  projects:
    env === 'dev'
      ? [
          {
            name: 'setup:auth',
            testMatch: /auth\.setup\.ts/,
          },
          {
            name: 'setup:seed',
            testMatch: /seed\.setup\.ts/,
            use: { storageState: authFile },
            dependencies: ['setup:auth'],
          },
          {
            name: 'chromium',
            testMatch: /real\/.+\.spec\.ts/,
            use: {
              ...devices['Desktop Chrome'],
              storageState: authFile,
              launchOptions: {
                executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ?? undefined,
              },
            },
            dependencies: ['setup:auth', 'setup:seed'],
          },
        ]
      : [
          {
            name: 'chromium',
            testMatch: /(?<!real\/).+\.spec\.ts/,
            use: {
              ...devices['Desktop Chrome'],
              launchOptions: {
                executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ?? undefined,
              },
            },
          },
        ],

  ...(env === 'local' && {
    webServer: {
      command: 'npm run dev -- --port 3100',
      url: `${baseURL}/projects`,
      reuseExistingServer: true,
      timeout: 120_000,
    },
  }),
});
