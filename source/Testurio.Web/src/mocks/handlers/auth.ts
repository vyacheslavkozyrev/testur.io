import { http, HttpResponse } from 'msw';
import type { AuthUser } from '@/types/layout.types';

export const mockAuthUser: AuthUser = {
  id: '00000000-0000-0000-0000-000000000099',
  displayName: 'Jane Smith',
  email: 'jane.smith@example.com',
  avatarUrl: undefined,
};

export const authHandlers = [
  http.get('/api/auth/me', () => HttpResponse.json(mockAuthUser)),
];
