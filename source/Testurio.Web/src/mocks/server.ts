import { setupServer } from 'msw/node';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';
import { projectAccessHandlers } from '@/mocks/handlers/projectAccess';

export const server = setupServer(...authHandlers, ...projectHandlers, ...projectAccessHandlers);
