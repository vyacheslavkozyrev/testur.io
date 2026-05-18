import { NextRequest, NextResponse } from 'next/server';

const CIAM_PROXY_PREFIX = '/ciam-proxy';

// Derive the CIAM base URL from env vars rather than hardcoding the tenant GUID.
// NEXT_PUBLIC_B2C_AUTHORITY = https://<tenant>.ciamlogin.com/<tenantId>/v2.0
// We strip /v2.0 to get the base: https://<tenant>.ciamlogin.com/<tenantId>
function getCiamBase(): string {
  const authority = process.env.NEXT_PUBLIC_B2C_AUTHORITY ?? '';
  return authority.replace(/\/v2\.0\/?$/, '');
}

const CIAM_BASE = getCiamBase();

// The app's own origin — used to restrict CORS on the proxy.
function getAppOrigin(): string {
  const redirectUri = process.env.NEXT_PUBLIC_B2C_REDIRECT_URI ?? '';
  try {
    return new URL(redirectUri).origin;
  } catch {
    return '';
  }
}

const APP_ORIGIN = getAppOrigin();

const PROXY_BODY_LIMIT = 64 * 1024; // 64 KB

export async function middleware(request: NextRequest): Promise<NextResponse> {
  if (request.nextUrl.pathname.startsWith(CIAM_PROXY_PREFIX)) {
    // Handle CORS preflight
    if (request.method === 'OPTIONS') {
      return new NextResponse(null, {
        status: 204,
        headers: {
          'Access-Control-Allow-Origin': APP_ORIGIN || '*',
          'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type, Accept',
          'Access-Control-Max-Age': '86400',
        },
      });
    }

    const upstreamPath = request.nextUrl.pathname.slice(CIAM_PROXY_PREFIX.length);
    const targetUrl = `${CIAM_BASE}${upstreamPath}${request.nextUrl.search}`;

    // Build a clean header set — do not forward client cookies or auth headers.
    const forwardHeaders = new Headers({
      'Content-Type': request.headers.get('content-type') ?? 'application/json',
      'Accept': request.headers.get('accept') ?? 'application/json',
      'host': new URL(CIAM_BASE).host,
    });

    const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
    let body: string | undefined;

    if (hasBody) {
      const raw = await request.text();
      if (raw.length > PROXY_BODY_LIMIT) {
        return NextResponse.json({ error: 'Request body too large' }, { status: 413 });
      }
      body = raw;
    }

    const upstream = await fetch(targetUrl, { method: request.method, headers: forwardHeaders, body });
    const responseText = await upstream.text();

    const responseHeaders = new Headers({
      'Access-Control-Allow-Origin': APP_ORIGIN || '*',
      'content-type': upstream.headers.get('content-type') ?? 'application/json',
    });

    return new NextResponse(responseText, { status: upstream.status, headers: responseHeaders });
  }

  // Auth guard for authenticated routes
  const sessionCookie = request.cookies.get('testurio_session');

  if (!sessionCookie?.value) {
    const returnUrl = encodeURIComponent(request.nextUrl.pathname);
    const signInUrl = new URL(`/sign-in?returnUrl=${returnUrl}`, request.url);
    return NextResponse.redirect(signInUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    '/ciam-proxy/:path*',
    '/dashboard/:path*',
    '/projects/:path*',
    '/settings/:path*',
  ],
};
