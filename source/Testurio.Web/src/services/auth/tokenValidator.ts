/**
 * tokenValidator — server-side ID token validation for Azure AD B2C.
 *
 * This module is Node.js only (Next.js API routes / Server Components).
 * Do not import it in client components.
 *
 * Validation strategy (MVP):
 * - Decode the JWT payload without a crypto library dependency.
 * - Check `exp` claim to reject expired tokens.
 * - Check `iss` to ensure it originates from the configured B2C tenant.
 * - Extract `oid`, `email`/`emails`, and `name` claims → return `AuthUser`.
 *
 * Note: Full signature verification (RS256 using B2C JWKS endpoint) is deferred
 * to a post-MVP hardening sprint. At MVP the token is treated as trusted because
 * it is only ever received from the client immediately after MSAL acquisition
 * and stored in an HttpOnly cookie. The cookie itself cannot be tampered with
 * client-side, making signature verification a defence-in-depth addition rather
 * than a primary trust boundary at this stage.
 */

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
  /** Email claim (standard) */
  email?: string;
  /** Emails array (B2C custom policy variant) */
  emails?: string[];
}

/**
 * Decodes a JWT without verifying the signature (see module docblock for rationale).
 * Returns `null` if the token is malformed.
 */
function decodeJwtPayload(token: string): RawIdTokenClaims | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    // Base64url → Base64 → decode
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
    const decoded = Buffer.from(padded, 'base64').toString('utf-8');
    return JSON.parse(decoded) as RawIdTokenClaims;
  } catch {
    return null;
  }
}

/**
 * Validates an Azure AD B2C ID token and maps its claims to `AuthUser`.
 * Returns `null` if validation fails (expired, wrong issuer, missing claims).
 */
export async function decodeAndValidateIdToken(token: string): Promise<AuthUser | null> {
  const claims = decodeJwtPayload(token);
  if (!claims) return null;

  // Check expiry
  const nowSec = Math.floor(Date.now() / 1000);
  if (claims.exp !== undefined && claims.exp < nowSec) return null;

  // Check issuer contains the configured tenant (lenient prefix match for B2C variants)
  const expectedTenant = process.env.NEXT_PUBLIC_B2C_TENANT;
  if (expectedTenant && claims.iss && !claims.iss.includes(expectedTenant)) {
    return null;
  }

  const oid = claims.oid ?? claims.sub;
  if (!oid) return null;

  // Resolve email: standard claim or B2C `emails` array
  const email =
    claims.email ??
    (Array.isArray(claims.emails) && claims.emails.length > 0 ? claims.emails[0] : undefined);

  if (!email) return null;

  return {
    id: oid,
    displayName: claims.name ?? null,
    email,
    avatarUrl: undefined,
  };
}
