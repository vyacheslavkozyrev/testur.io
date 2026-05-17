import { NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { decodeAndValidateIdToken } from '@/services/auth/tokenValidator';

/**
 * GET /api/auth/me
 *
 * Reads the `testurio_session` HttpOnly cookie, validates the B2C ID token
 * contained within it, and returns the signed-in user's identity.
 *
 * Returns 401 Unauthorized if:
 * - The cookie is absent
 * - The token is expired or malformed
 * - Token validation fails for any reason
 */
export async function GET(): Promise<NextResponse> {
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');

  if (!sessionCookie?.value) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const user = await decodeAndValidateIdToken(sessionCookie.value);
  if (!user) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  return NextResponse.json(user);
}
