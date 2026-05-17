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

  // Build the B2C v2 logout URL.
  // The correct path for Azure AD B2C is /oauth2/v2.0/logout relative to the authority.
  // post_logout_redirect_uri sends the user to /sign-in after B2C clears its session.
  let logoutUrl: string;
  try {
    const url = new URL(`${authority.replace(/\/$/, '')}/oauth2/v2.0/logout`);
    url.searchParams.set(
      'post_logout_redirect_uri',
      `${process.env.NEXT_PUBLIC_B2C_REDIRECT_URI?.replace('/auth/callback', '') ?? ''}/sign-in`,
    );
    logoutUrl = url.toString();
  } catch {
    // Fallback if env vars are not configured (e.g. local dev without B2C)
    logoutUrl = '/sign-in';
  }

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
