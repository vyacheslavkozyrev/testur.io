import { setupWorker } from 'msw/browser';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';
import { pmToolHandlers } from '@/mocks/handlers/pmTool';

export const worker = setupWorker(...authHandlers, ...projectHandlers, ...pmToolHandlers);
