import { setupServer } from 'msw/node';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';

export const server = setupServer(...authHandlers, ...projectHandlers);
