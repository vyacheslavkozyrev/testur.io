import { NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { getSessionStore } from '@/app/api/auth/session/route';

/**
 * POST /api/auth/sign-out
 *
 * Clears the `testurio_session` HttpOnly cookie, removes the server-side session,
 * and returns the B2C logout URL for the client to redirect to.
 *
 * The client is responsible for navigating to `logoutUrl` after this call.
 * If the navigation fails, the local session is still cleared so the user
 * is not left in a broken authenticated state.
 *
 * Expected env var format:
 *   NEXT_PUBLIC_B2C_AUTHORITY — full authority URL, e.g.
 *     https://<tenant>.b2clogin.com/<tenant>.onmicrosoft.com/B2C_1_susi
 *
 * The B2C v2 logout endpoint is at: <authority>/oauth2/v2.0/logout
 */
export async function POST(): Promise<NextResponse> {
  const authority = process.env.NEXT_PUBLIC_B2C_AUTHORITY ?? '';

  // Remove the server-side session entry
  const cookieStore = await cookies();
  const sessionCookie = cookieStore.get('testurio_session');
  if (sessionCookie?.value) {
    getSessionStore().delete(sessionCookie.value);
  }

  // Native auth does not create a browser-side Azure session, so there is no
  // CIAM logout endpoint to call. Simply redirect to /sign-in — the server-side
  // session and cookie have already been cleared above.
  const logoutUrl = '/sign-in';
  void authority; // env var retained for future use

  const response = NextResponse.json({ logoutUrl });

  // Clear the session cookie by setting it with an empty value and maxAge=0
  response.cookies.set('testurio_session', '', {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'strict',
    path: '/',
    maxAge: 0,
  });

  return response;
}
