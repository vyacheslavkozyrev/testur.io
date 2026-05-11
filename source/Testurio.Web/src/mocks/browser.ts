import { setupWorker } from 'msw/browser';
import { projectHandlers } from '@/mocks/handlers/project';

export const worker = setupWorker(...projectHandlers);
