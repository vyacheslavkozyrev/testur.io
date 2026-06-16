import { test as setup } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const localAuthFile = path.join(__dirname, '.auth/local.json');

setup('create local test session', async ({ page }) => {
  fs.mkdirSync(path.dirname(localAuthFile), { recursive: true });

  const res = await page.request.post('/api/test/session');
  if (!res.ok()) {
    throw new Error(`Failed to create local test session: ${res.status()} ${await res.text()}`);
  }

  await page.context().storageState({ path: localAuthFile });
});
