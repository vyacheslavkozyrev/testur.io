'use client';

import { useEffect, useState } from 'react';
import type { AuthUser } from '@/types/layout.types';

/**
 * Returns the signed-in user identity from the Azure AD B2C session.
 * The session is exposed via a cookie read by the Next.js API route `/api/auth/me`,
 * which decodes the B2C ID token and returns the user claims.
 *
 * Returns `null` while loading or when no valid session is present.
 */
export function useAuthUser(): AuthUser | null {
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    fetch('/api/auth/me')
      .then((res) => (res.ok ? res.json() : null))
      .then((data: AuthUser | null) => setUser(data))
      .catch(() => setUser(null));
  }, []);

  return user;
}
