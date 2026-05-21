import { http, HttpResponse } from 'msw';
import type { AuthUser } from '@/types/layout.types';

export const mockAuthUser: AuthUser = {
  id: '00000000-0000-0000-0000-000000000099',
  firstName: 'Jane',
  lastName: 'Smith',
  displayName: null,
  email: 'jane.smith@example.com',
  avatarUrl: undefined,
};

/** A mock raw ID token string used by POST /api/auth/session tests. */
export const mockIdToken = 'mock.id.token';

export const authHandlers = [
  /** GET /api/auth/me — returns the mock authenticated user */
  http.get('/api/auth/me', () => HttpResponse.json(mockAuthUser)),

  /**
   * POST /api/auth/session — accepts a mock ID token and returns the mock user.
   * Tests that need to simulate an invalid token can override this handler.
   */
  http.post('/api/auth/session', () => HttpResponse.json(mockAuthUser)),

  /**
   * POST /api/auth/sign-out — returns the sign-in page as the logout URL.
   * In component tests the caller is expected to redirect to this URL.
   */
  http.post('/api/auth/sign-out', () =>
    HttpResponse.json({ logoutUrl: '/sign-in' }),
  ),
];
