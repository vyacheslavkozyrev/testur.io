import { NextRequest, NextResponse } from 'next/server';

const CIAM_PROXY_PREFIX = '/ciam-proxy';
const CIAM_BASE = 'https://testuriob2cdevelop.ciamlogin.com/987723e6-b3c0-48b1-a2d0-2ca3520581d5';

export async function middleware(request: NextRequest): Promise<NextResponse> {
  console.log('[middleware] path:', request.nextUrl.pathname);

  // CIAM native-auth proxy — forward to Azure to avoid CORS restrictions
  if (request.nextUrl.pathname.startsWith(CIAM_PROXY_PREFIX)) {
    const upstreamPath = request.nextUrl.pathname.slice(CIAM_PROXY_PREFIX.length);
    const targetUrl = `${CIAM_BASE}${upstreamPath}${request.nextUrl.search}`;

    const headers = new Headers(request.headers);
    headers.set('host', new URL(CIAM_BASE).host);
    headers.delete('x-forwarded-for');
    headers.delete('x-forwarded-host');
    headers.delete('x-forwarded-proto');

    const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
    const body = hasBody ? await request.text() : undefined;

    const upstream = await fetch(targetUrl, { method: request.method, headers, body });
    const responseText = await upstream.text();

    if (!upstream.ok) {
      console.error(`[ciam-proxy] ${upstream.status} from ${targetUrl}:`, responseText);
    }

    const responseHeaders = new Headers(upstream.headers);
    responseHeaders.set('Access-Control-Allow-Origin', request.headers.get('origin') ?? '*');
    responseHeaders.set('content-type', upstream.headers.get('content-type') ?? 'application/json');

    return new NextResponse(responseText, {
      status: upstream.status,
      headers: responseHeaders,
    });
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
