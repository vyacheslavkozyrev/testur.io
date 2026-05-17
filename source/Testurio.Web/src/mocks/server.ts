import { setupServer } from 'msw/node';
import { authHandlers } from '@/mocks/handlers/auth';
import { projectHandlers } from '@/mocks/handlers/project';
import { projectAccessHandlers } from '@/mocks/handlers/projectAccess';
import { projectApiAuthHandlers } from '@/mocks/handlers/projectApiAuth';
import { plansHandlers } from '@/mocks/handlers/plans';

export const server = setupServer(...authHandlers, ...projectHandlers, ...projectAccessHandlers, ...projectApiAuthHandlers, ...plansHandlers);
