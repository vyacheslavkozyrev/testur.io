import { setupWorker } from 'msw/browser';
import { projectHandlers } from '@/mocks/handlers/project';
import { pmToolHandlers } from '@/mocks/handlers/pmTool';

export const worker = setupWorker(...projectHandlers, ...pmToolHandlers);
