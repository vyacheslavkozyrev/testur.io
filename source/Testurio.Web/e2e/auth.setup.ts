import { test as setup } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const authFile = path.join(__dirname, '.auth/user.json');

setup('authenticate', async ({ page }) => {
  fs.mkdirSync(path.dirname(authFile), { recursive: true });

  // Inject Cloudflare Access service token headers on every request so the
  // CF Access gate passes the browser through without the GitHub OAuth prompt.
  await page.route('**/*', (route) =>
    route.continue({
      headers: {
        ...route.request().headers(),
        'CF-Access-Client-Id':     process.env.CF_ACCESS_CLIENT_ID ?? '',
        'CF-Access-Client-Secret': process.env.CF_ACCESS_CLIENT_SECRET ?? '',
      },
    }),
  );

  await page.goto('/sign-in', { waitUntil: 'networkidle' });

  console.log('Current URL after navigation:', page.url());

  const emailInput = page.locator('input[name="email"]');
  await emailInput.waitFor({ state: 'visible', timeout: 30_000 });
  await emailInput.fill(process.env.TEST_USER_EMAIL!);
  await page.locator('input[name="password"]').fill(process.env.TEST_USER_PASSWORD!);
  await page.getByRole('button', { name: 'Sign In' }).click();

  await page.waitForURL('**/dashboard', { timeout: 30_000 });

  await page.context().storageState({ path: authFile });
});
