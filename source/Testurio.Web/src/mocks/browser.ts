import { setupWorker } from 'msw/browser';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';
import { pmToolHandlers } from '@/mocks/handlers/pmTool';
import { dashboardHandlers } from '@/mocks/handlers/dashboard';
import { projectAccessHandlers } from '@/mocks/handlers/projectAccess';

export const worker = setupWorker(
  ...authHandlers,
  ...projectHandlers,
  ...pmToolHandlers,
  ...dashboardHandlers,
  ...projectAccessHandlers,
);
