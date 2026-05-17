import { NextRequest, NextResponse } from 'next/server';

/**
 * Next.js Middleware — authentication guard for all authenticated routes.
 *
 * Runs on the Edge before any page component renders.
 * - If the `testurio_session` cookie is present, the request passes through.
 * - If the cookie is absent, the user is redirected to /sign-in with the
 *   original path as `returnUrl` so they land back after signing in.
 *
 * Note: The middleware only checks cookie presence. Full session validation
 * (expiry, server-side store lookup) happens in `(authenticated)/layout.tsx`
 * and `/api/auth/me`, providing defence-in-depth.
 */
export function middleware(request: NextRequest): NextResponse {
  const sessionCookie = request.cookies.get('testurio_session');

  if (!sessionCookie?.value) {
    const returnUrl = encodeURIComponent(request.nextUrl.pathname);
    const signInUrl = new URL(`/sign-in?returnUrl=${returnUrl}`, request.url);
    return NextResponse.redirect(signInUrl);
  }

  return NextResponse.next();
}

export const config = {
  /**
   * Match all routes under the (authenticated) group.
   * Excludes API routes, static files, and Next.js internals.
   */
  matcher: [
    '/dashboard/:path*',
    '/projects/:path*',
    '/settings/:path*',
  ],
};
