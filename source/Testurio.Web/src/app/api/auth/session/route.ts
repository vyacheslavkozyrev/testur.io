import { NextRequest, NextResponse } from 'next/server';
import { decodeAndValidateIdToken } from '@/services/auth/tokenValidator';

/** Cookie max-age in seconds — 7 days */
const SESSION_MAX_AGE_SECONDS = 60 * 60 * 24 * 7;

/**
 * POST /api/auth/session
 *
 * Receives a raw B2C ID token from the client (obtained via MSAL after sign-in or sign-up),
 * validates it, and sets a `testurio_session` HttpOnly cookie containing the raw token.
 *
 * Returns the `AuthUser` payload so the client can update its local state immediately
 * without a follow-up `GET /api/auth/me` call.
 *
 * Returns 401 if the token is missing or fails validation.
 * Returns 400 if the request body is malformed.
 */
export async function POST(request: NextRequest): Promise<NextResponse> {
  let body: { idToken?: string };
  try {
    body = await request.json() as { idToken?: string };
  } catch {
    return NextResponse.json({ error: 'Invalid request body' }, { status: 400 });
  }

  const { idToken } = body;
  if (!idToken || typeof idToken !== 'string') {
    return NextResponse.json({ error: 'idToken is required' }, { status: 400 });
  }

  const user = await decodeAndValidateIdToken(idToken);
  if (!user) {
    return NextResponse.json({ error: 'Invalid or expired ID token' }, { status: 401 });
  }

  const response = NextResponse.json(user);

  response.cookies.set('testurio_session', idToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    path: '/',
    maxAge: SESSION_MAX_AGE_SECONDS,
  });

  return response;
}
