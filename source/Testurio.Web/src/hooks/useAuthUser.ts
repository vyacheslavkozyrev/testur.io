'use client';

import { useEffect, useState } from 'react';
import { authService } from '@/services/auth/authService';
import type { AuthUser } from '@/types/layout.types';

/**
 * Returns the signed-in user identity from the server-side session.
 * Calls GET /api/auth/me on the Next.js server (not the .NET backend).
 * Returns `null` while loading or when no valid session is present.
 */
export function useAuthUser(): AuthUser | null {
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    authService.getSession().then(setUser);
  }, []);

  return user;
}
