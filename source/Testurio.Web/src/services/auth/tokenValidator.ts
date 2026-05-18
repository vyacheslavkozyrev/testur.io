/**
 * tokenValidator — server-side ID token validation for Azure AD B2C.
 *
 * This module is Node.js only (Next.js API routes / Server Components).
 * Do not import it in client components.
 *
 * Validation strategy:
 * - Verify the RS256 signature using the B2C JWKS endpoint.
 * - JWKS is cached via jose's built-in RemoteJWKSet caching.
 * - Check `exp` claim to reject expired tokens.
 * - Check `iss` claim against the exact full B2C issuer URL.
 * - Extract `oid`, `email`/`emails`, and `name` claims → return `AuthUser`.
 *
 * Expected env vars:
 *   NEXT_PUBLIC_B2C_AUTHORITY  — full authority URL, e.g.
 *     https://<tenant>.b2clogin.com/<tenant>.onmicrosoft.com/B2C_1_susi
 *   NEXT_PUBLIC_B2C_TENANT     — tenant domain, e.g. <tenant>.onmicrosoft.com
 */

import { createRemoteJWKSet, jwtVerify } from 'jose';
import type { AuthUser } from '@/types/layout.types';

interface RawIdTokenClaims {
  /** Issuer — B2C authority URL */
  iss?: string;
  /** Subject — same as oid for B2C */
  sub?: string;
  /** Expiry Unix timestamp */
  exp?: number;
  /** B2C Object ID — stable user identifier */
  oid?: string;
  /** Display name from B2C profile */
  name?: string;
  /** Given name (first name) */
  given_name?: string;
  /** Surname (last name) */
  family_name?: string;
  /** Email claim (standard) */
  email?: string;
  /** Emails array (B2C custom policy variant) */
  emails?: string[];
}

/**
 * Lazily-initialised RemoteJWKSet. jose caches the JWKS internally and
 * re-fetches only when a matching key is not found (standard JWK cache
 * semantics), so we reuse a single instance across requests.
 */
let _jwks: ReturnType<typeof createRemoteJWKSet> | null = null;

function getTenantId(): string {
  const authority = process.env.NEXT_PUBLIC_B2C_AUTHORITY ?? '';
  // Authority format: https://<tenant>.ciamlogin.com/<tenantId>/v2.0
  const match = authority.match(/\/([0-9a-f-]{36})\/v2\.0/i);
  return match?.[1] ?? '';
}

function getJwks(): ReturnType<typeof createRemoteJWKSet> {
  if (!_jwks) {
    const tenantId = getTenantId();
    // CIAM JWKS uses the tenant GUID as subdomain
    const jwksUrl = new URL(`https://${tenantId}.ciamlogin.com/${tenantId}/discovery/v2.0/keys`);
    _jwks = createRemoteJWKSet(jwksUrl);
  }
  return _jwks;
}

/**
 * Validates an Azure AD B2C ID token and maps its claims to `AuthUser`.
 *
 * - Verifies the RS256 signature against the B2C JWKS endpoint.
 * - Checks the `iss` claim with an exact full-URL match.
 * - Checks the `exp` claim (jose rejects expired tokens automatically).
 *
 * Returns `null` if validation fails for any reason.
 */
export async function decodeAndValidateIdToken(token: string): Promise<AuthUser | null> {
  try {
    const jwks = getJwks();

    const { payload } = await jwtVerify(token, jwks, {
      algorithms: ['RS256'],
    });

    const claims = payload as unknown as RawIdTokenClaims;

    // CIAM issues tokens with iss = https://<tenantId>.ciamlogin.com/<tenantId>/v2.0 (no trailing slash)
    const tenantId = getTenantId();
    const expectedIssuer = `https://${tenantId}.ciamlogin.com/${tenantId}/v2.0`;
    if (claims.iss !== expectedIssuer) return null;

    const oid = claims.oid ?? claims.sub;
    if (!oid) return null;

    // Resolve email: standard claim or B2C `emails` array
    const email =
      claims.email ??
      (Array.isArray(claims.emails) && claims.emails.length > 0 ? claims.emails[0] : undefined);

    if (!email) return null;

    const displayName = claims.name
      ?? ([claims.given_name, claims.family_name].filter(Boolean).join(' ') || null);

    return {
      id: oid,
      displayName,
      email,
      avatarUrl: undefined,
    };
  } catch {
    return null;
  }
}
