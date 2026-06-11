import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { getSessionStore } from '@/app/api/auth/session/store';

const API_BASE_URL = process.env.API_BASE_URL ?? 'http://localhost:8080';

async function proxyRequest(
  request: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
): Promise<NextResponse> {
  const { path } = await params;

  const cookieStore = await cookies();
  const sessionId = cookieStore.get('testurio_session')?.value;
  const session = sessionId ? getSessionStore().get(sessionId) : undefined;

  const upstream = new URL(`/v1/${path.join('/')}`, API_BASE_URL);
  request.nextUrl.searchParams.forEach((value, key) => {
    upstream.searchParams.append(key, value);
  });

  const headers: HeadersInit = {};
  const contentType = request.headers.get('Content-Type');
  if (contentType) headers['Content-Type'] = contentType;
  if (session?.idToken) headers['Authorization'] = `Bearer ${session.idToken}`;

  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
  const body = hasBody ? await request.text() : undefined;

  const upstreamResponse = await fetch(upstream.toString(), {
    method: request.method,
    headers,
    body,
  });

  const responseHeaders = new Headers();
  const responseContentType = upstreamResponse.headers.get('Content-Type');
  if (responseContentType) responseHeaders.set('Content-Type', responseContentType);
  const cacheControl = upstreamResponse.headers.get('Cache-Control');
  if (cacheControl) responseHeaders.set('Cache-Control', cacheControl);

  return new NextResponse(upstreamResponse.body, {
    status: upstreamResponse.status,
    headers: responseHeaders,
  });
}

export const GET = proxyRequest;
export const POST = proxyRequest;
export const PUT = proxyRequest;
export const PATCH = proxyRequest;
export const DELETE = proxyRequest;
