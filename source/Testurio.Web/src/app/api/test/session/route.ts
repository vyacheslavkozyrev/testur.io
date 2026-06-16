import { NextResponse } from 'next/server';
import { persistSession } from '@/app/api/auth/session/store';

/**
 * POST /api/test/session
 *
 * Dev/test only — creates a server-side session for a synthetic E2E test user
 * without requiring a real Azure B2C JWT. Returns a testurio_session cookie so
 * Playwright local tests can pass both the middleware and layout auth guards.
 *
 * Returns 404 in production.
 */
export async function POST(): Promise<NextResponse> {
  if (process.env.NODE_ENV === 'production') {
    return NextResponse.json({ error: 'Not found' }, { status: 404 });
  }

  const sessionId = crypto.randomUUID();
  const exp = Math.floor(Date.now() / 1000) + 3600;

  persistSession(sessionId, {
    userId: 'e2e-local-test-user-00000000000000000001',
    firstName: 'E2E',
    lastName: 'Local',
    email: 'e2e-local@example.com',
    displayName: 'E2E Local',
    avatarUrl: undefined,
    exp,
    idToken: '',
  });

  const response = NextResponse.json({ ok: true });
  response.cookies.set('testurio_session', sessionId, {
    httpOnly: true,
    secure: false,
    sameSite: 'strict',
    path: '/',
    maxAge: 3600,
  });
  return response;
}
