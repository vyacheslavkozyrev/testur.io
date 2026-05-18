import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.BASE_URL ?? 'http://localhost:3100';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: 'html',
  use: {
    baseURL,
    trace: 'on-first-retry',
    navigationTimeout: 60_000,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          executablePath:
            process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH ??
            'C:/Users/vyach/AppData/Local/ms-playwright/chromium-1208/chrome-win64/chrome.exe',
        },
      },
    },
  ],
  webServer: {
    command: 'npm run dev -- --port 3100',
    url: `${baseURL}/projects`,
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
