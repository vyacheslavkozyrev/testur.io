import { NextRequest, NextResponse } from 'next/server';
import { decodeAndValidateIdToken } from '@/services/auth/tokenValidator';
import type { AuthUser } from '@/types/layout.types';
import { getSessionStore } from './store';

/** Evicts all expired sessions from the store. Called lazily on each write. */
function evictExpired(): void {
  const store = getSessionStore();
  const nowSec = Math.floor(Date.now() / 1000);
  for (const [id, session] of store) {
    if (session.exp < nowSec) store.delete(id);
  }
}

function createSessionResponse(user: AuthUser, exp: number, idToken: string): NextResponse {
  evictExpired();

  const sessionId = crypto.randomUUID();
  const nowSec = Math.floor(Date.now() / 1000);

  getSessionStore().set(sessionId, {
    userId: user.id,
    firstName: user.firstName ?? null,
    lastName: user.lastName ?? null,
    email: user.email,
    displayName: user.displayName ?? null,
    avatarUrl: user.avatarUrl,
    exp,
    idToken,
  });

  const response = NextResponse.json(user);
  response.cookies.set('testurio_session', sessionId, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'strict',
    path: '/',
    maxAge: Math.max(0, exp - nowSec),
  });
  return response;
}

/**
 * POST /api/auth/session
 *
 * Receives a raw B2C ID token (obtained via MSAL native auth after sign-in or sign-up),
 * validates its RS256 signature and claims, stores session data server-side, and sets
 * a `testurio_session` HttpOnly cookie containing only a randomly-generated session ID.
 *
 * Optionally accepts `firstName` and `lastName` as supplementary display data alongside
 * the token — these are not used for identity (oid and email always come from the
 * verified JWT) but fill in name fields when CIAM does not include them as token claims.
 *
 * Returns 401 if the token is missing or fails validation.
 * Returns 400 if the request body is malformed.
 */
export async function POST(request: NextRequest): Promise<NextResponse> {
  let body: { idToken?: string; firstName?: string | null; lastName?: string | null };
  try {
    body = await request.json() as { idToken?: string; firstName?: string | null; lastName?: string | null };
  } catch {
    return NextResponse.json({ error: 'Invalid request body' }, { status: 400 });
  }

  const { idToken } = body;
  if (!idToken || typeof idToken !== 'string') {
    return NextResponse.json({ error: 'idToken is required' }, { status: 400 });
  }

  const tokenUser = await decodeAndValidateIdToken(idToken);
  if (!tokenUser) {
    return NextResponse.json({ error: 'Invalid or expired ID token' }, { status: 401 });
  }

  // Prefer client-supplied names when the token does not include them as claims
  // (CIAM omits given_name/family_name unless they are configured as ID token claims).
  // These are display-only fields — identity always comes from the verified JWT.
  const firstName = body.firstName ?? tokenUser.firstName ?? null;
  const lastName = body.lastName ?? tokenUser.lastName ?? null;
  const displayName = tokenUser.displayName
    ?? ([firstName, lastName].filter(Boolean).join(' ') || null);

  const user: AuthUser = {
    id: tokenUser.id,
    firstName,
    lastName,
    displayName,
    email: tokenUser.email,
    avatarUrl: tokenUser.avatarUrl,
  };

  const tokenParts = idToken.split('.');
  let exp: number = Math.floor(Date.now() / 1000) + 60 * 60 * 24;
  if (tokenParts.length === 3) {
    try {
      const payload = tokenParts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
      const decoded = JSON.parse(Buffer.from(padded, 'base64').toString('utf-8')) as { exp?: number };
      if (typeof decoded.exp === 'number') exp = decoded.exp;
    } catch { /* use default */ }
  }

  return createSessionResponse(user, exp, idToken);
}
