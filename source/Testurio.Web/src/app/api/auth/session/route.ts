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

// Anchored to globalThis so the store survives Next.js hot-module replacement in development.
const g = globalThis as { _testurioSessions?: Map<string, SessionData> };
if (!g._testurioSessions) g._testurioSessions = new Map<string, SessionData>();
const sessionStore = g._testurioSessions;

/** Returns the session store (exported for use by /api/auth/me and /api/auth/sign-out). */
export function getSessionStore(): Map<string, SessionData> {
  return g._testurioSessions!;
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
interface NativeClaims {
  oid: string;
  email?: string;
  name?: string;
}

function createSessionResponse(user: AuthUser, exp: number): NextResponse {
  const sessionId = crypto.randomUUID();
  const nowSec = Math.floor(Date.now() / 1000);

  sessionStore.set(sessionId, {
    userId: user.id,
    email: user.email,
    displayName: user.displayName ?? null,
    avatarUrl: user.avatarUrl,
    exp,
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

export async function POST(request: NextRequest): Promise<NextResponse> {
  let body: { idToken?: string; nativeClaims?: NativeClaims };
  try {
    body = await request.json() as { idToken?: string; nativeClaims?: NativeClaims };
  } catch {
    return NextResponse.json({ error: 'Invalid request body' }, { status: 400 });
  }

  // Native auth path — MSAL already authenticated the user; use claims directly
  if (body.nativeClaims) {
    const { oid, email, name } = body.nativeClaims;
    if (!oid) return NextResponse.json({ error: 'oid claim is required' }, { status: 400 });

    const exp = Math.floor(Date.now() / 1000) + 60 * 60 * 24; // 24h session
    const user: AuthUser = { id: oid, email: email ?? '', displayName: name ?? null };
    return createSessionResponse(user, exp);
  }

  // ID token path — validate JWT signature and claims
  const { idToken } = body;
  if (!idToken || typeof idToken !== 'string') {
    return NextResponse.json({ error: 'idToken or nativeClaims is required' }, { status: 400 });
  }

  const user = await decodeAndValidateIdToken(idToken);
  if (!user) {
    return NextResponse.json({ error: 'Invalid or expired ID token' }, { status: 401 });
  }

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

  return createSessionResponse(user, exp);
}
