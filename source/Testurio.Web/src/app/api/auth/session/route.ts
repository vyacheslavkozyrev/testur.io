import { NextRequest, NextResponse } from 'next/server';
import { decodeAndValidateIdToken } from '@/services/auth/tokenValidator';
import type { AuthUser } from '@/types/layout.types';

/**
 * Server-side session store (in-memory, keyed by session ID).
 *
 * Stores `{userId, email, displayName, exp}` — the raw ID token is never
 * persisted in the cookie. Only the opaque session ID travels to the client.
 *
 * Note: In-memory store is cleared on process restart. For production, replace
 * with a Redis or Cosmos-backed store.
 */
interface SessionData {
  userId: string;
  email: string;
  displayName: string | null;
  avatarUrl?: string;
  exp: number;
}

const sessionStore = new Map<string, SessionData>();

/** Returns the session store (exported for use by /api/auth/me and /api/auth/sign-out). */
export function getSessionStore(): Map<string, SessionData> {
  return sessionStore;
}

/**
 * POST /api/auth/session
 *
 * Receives a raw B2C ID token from the client (obtained via MSAL after sign-in or sign-up),
 * validates it, stores session data server-side, and sets a `testurio_session` HttpOnly cookie
 * containing only a randomly-generated session ID.
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

  // Decode exp from the token payload for cookie maxAge
  const tokenParts = idToken.split('.');
  let exp: number = Math.floor(Date.now() / 1000) + 60 * 60 * 24; // default 24h
  if (tokenParts.length === 3) {
    try {
      const payload = tokenParts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
      const decoded = JSON.parse(Buffer.from(padded, 'base64').toString('utf-8')) as { exp?: number };
      if (typeof decoded.exp === 'number') {
        exp = decoded.exp;
      }
    } catch {
      // use default exp
    }
  }

  // Generate an opaque session ID — only this is stored in the cookie
  const sessionId = crypto.randomUUID();

  sessionStore.set(sessionId, {
    userId: user.id,
    email: user.email,
    displayName: user.displayName ?? null,
    avatarUrl: user.avatarUrl,
    exp,
  });

  const nowSec = Math.floor(Date.now() / 1000);
  const maxAge = Math.max(0, exp - nowSec);

  const response = NextResponse.json(user);

  response.cookies.set('testurio_session', sessionId, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'strict',
    path: '/',
    maxAge,
  });

  return response;
}
