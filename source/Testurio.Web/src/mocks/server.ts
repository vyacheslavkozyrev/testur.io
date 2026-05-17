import { setupServer } from 'msw/node';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';
import { projectAccessHandlers } from '@/mocks/handlers/projectAccess';
import { projectApiAuthHandlers } from '@/mocks/handlers/projectApiAuth';

export const server = setupServer(...authHandlers, ...projectHandlers, ...projectAccessHandlers, ...projectApiAuthHandlers);
