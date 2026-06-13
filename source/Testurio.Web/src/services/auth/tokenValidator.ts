/**
 * tokenValidator — server-side ID token validation for Azure AD B2C.
 *
 * This module is Node.js only (Next.js API routes / Server Components).
 * Do not import it in client components.
 */

import { createRemoteJWKSet, jwtVerify } from 'jose';
import type { AuthUser } from '@/types/layout.types';

interface RawIdTokenClaims {
  iss?: string;
  sub?: string;
  exp?: number;
  oid?: string;
  name?: string;
  given_name?: string;
  family_name?: string;
  email?: string;
  emails?: string[];
}

// ─── Tenant ID ────────────────────────────────────────────────────────────────

function resolveTenantId(): string {
  const authority = process.env.NEXT_PUBLIC_B2C_AUTHORITY ?? '';
  const match = authority.match(/\/([0-9a-f-]{36})\/v2\.0/i);
  const tenantId = match?.[1] ?? '';
  if (!tenantId && process.env.NODE_ENV !== 'test') {
    throw new Error(
      '[tokenValidator] Cannot extract tenant ID from NEXT_PUBLIC_B2C_AUTHORITY. ' +
      'Expected format: https://<tenant>.ciamlogin.com/<tenantId-guid>/v2.0',
    );
  }
  return tenantId;
}

const TENANT_ID = resolveTenantId();
const EXPECTED_ISSUER = `https://${TENANT_ID}.ciamlogin.com/${TENANT_ID}/v2.0`;
const AUDIENCE = process.env.NEXT_PUBLIC_B2C_CLIENT_ID ?? '';

// ─── JWKS ─────────────────────────────────────────────────────────────────────

let _jwks: ReturnType<typeof createRemoteJWKSet> | null = null;

function getJwks(): ReturnType<typeof createRemoteJWKSet> {
  if (!_jwks) {
    const jwksUrl = new URL(
      `https://${TENANT_ID}.ciamlogin.com/${TENANT_ID}/discovery/v2.0/keys`,
    );
    _jwks = createRemoteJWKSet(jwksUrl);
  }
  return _jwks;
}

// ─── Validation ───────────────────────────────────────────────────────────────

/**
 * Validates an Azure AD B2C ID token and maps its claims to `AuthUser`.
 * Returns `null` if validation fails for any reason.
 */
export async function decodeAndValidateIdToken(token: string): Promise<AuthUser | null> {
  try {
    const { payload } = await jwtVerify(token, getJwks(), {
      algorithms: ['RS256'],
      audience: AUDIENCE,
    });

    const claims = payload as unknown as RawIdTokenClaims;

    if (claims.iss !== EXPECTED_ISSUER) return null;

    const oid = claims.sub ?? claims.oid;
    if (!oid) return null;

    const email =
      claims.email ??
      (Array.isArray(claims.emails) && claims.emails.length > 0 ? claims.emails[0] : undefined);

    if (!email) return null;

    const firstName = claims.given_name ?? null;
    const lastName = claims.family_name ?? null;
    const displayName = claims.name
      ?? ([firstName, lastName].filter(Boolean).join(' ') || null);

    return { id: oid, firstName, lastName, displayName, email, avatarUrl: undefined };
  } catch {
    return null;
  }
}
