import { NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { getSessionStore } from '@/app/api/auth/session/store';

/**
 * GET /api/auth/me
 *
 * Reads the `testurio_session` HttpOnly cookie, looks up the session ID in the
 * server-side session store, and returns the signed-in user's identity.
 *
 * Returns 401 Unauthorized if:
 * - The cookie is absent
 * - The session ID is not found in the store
 * - The session has expired
 */
export async function GET(): Promise<NextResponse> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');

  if (!sessionCookie?.value) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const sessionStore = getSessionStore();
  const session = sessionStore.get(sessionCookie.value);

  if (!session) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  // Check session expiry
  const nowSec = Math.floor(Date.now() / 1000);
  if (session.exp < nowSec) {
    sessionStore.delete(sessionCookie.value);
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  return NextResponse.json({
    id: session.userId,
    firstName: session.firstName,
    lastName: session.lastName,
    email: session.email,
    displayName: session.displayName,
    avatarUrl: session.avatarUrl,
  });
}
