import { setupWorker } from 'msw/browser';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';
import { pmToolHandlers } from '@/mocks/handlers/pmTool';
import { dashboardHandlers } from '@/mocks/handlers/dashboard';

export const worker = setupWorker(
  ...authHandlers,
  ...projectHandlers,
  ...pmToolHandlers,
  ...dashboardHandlers,
);
