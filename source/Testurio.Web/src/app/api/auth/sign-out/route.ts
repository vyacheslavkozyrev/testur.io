import { NextResponse } from 'next/server';

/**
 * POST /api/auth/sign-out
 *
 * Clears the `testurio_session` HttpOnly cookie and returns the B2C logout URL
 * for the client to redirect to.
 *
 * The client is responsible for navigating to `logoutUrl` after this call.
 * If the navigation fails, the local session is still cleared so the user
 * is not left in a broken authenticated state.
 */
export async function POST(): Promise<NextResponse> {
  const authority = process.env.NEXT_PUBLIC_B2C_AUTHORITY ?? '';
  const baseUrl = process.env.NEXT_PUBLIC_B2C_KNOWN_AUTHORITY ?? '';

  // Build the B2C v2 logout URL.
  // post_logout_redirect_uri sends the user to /sign-in after B2C clears its session.
  let logoutUrl: string;
  try {
    const url = new URL('/v2.0/logout', authority || baseUrl);
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
    sameSite: 'lax',
    path: '/',
    maxAge: 0,
  });

  return response;
}
