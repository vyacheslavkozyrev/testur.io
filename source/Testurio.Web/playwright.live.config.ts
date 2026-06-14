import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';
import * as path from 'path';

dotenv.config({ path: path.resolve(__dirname, '../../.env.test') });

const baseURL = process.env.BASE_URL!;
const authFile = path.join(__dirname, 'e2e/.auth/user.json');

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: 'html',
  globalTeardown: path.join(__dirname, 'e2e/real/teardown.ts'),
  use: {
    baseURL,
    trace: 'on-first-retry',
    navigationTimeout: 60_000,
    // CF-Access headers are required when running against the dev environment.
    // Ignored by localhost. Browser navigations get them via page.route() in auth.setup.ts.
    extraHTTPHeaders: {
      'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
      'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
    },
  },
  projects: [
    {
      name: 'setup:auth',
      testMatch: /auth\.setup\.ts/,
    },
    {
      name: 'setup:seed',
      testMatch: /seed\.setup\.ts/,
      use: {
        storageState: authFile,
      },
      dependencies: ['setup:auth'],
    },
    {
      name: 'chromium',
      testMatch: /real\/.+\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        storageState: authFile,
        launchOptions: {
          executablePath:
            process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ??
            'C:/Users/vyach/AppData/Local/ms-playwright/chromium-1208/chrome-win64/chrome.exe',
        },
      },
      dependencies: ['setup:auth', 'setup:seed'],
    },
  ],
});
