import { setupServer } from 'msw/node';
import { projectHandlers } from '@/mocks/handlers/project';

export const server = setupServer(...projectHandlers);
