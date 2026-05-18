'use client';

import { useQuery } from '@tanstack/react-query';
import { authService } from '@/services/auth/authService';
import { AUTH_KEYS } from '@/hooks/useAuth';
import type { AuthUser } from '@/types/layout.types';

/**
 * Returns the signed-in user identity from the server-side session.
 * Deduplicates requests across all component instances via React Query.
 * Returns `null` while loading or when no valid session is present.
 */
export function useAuthUser(): AuthUser | null {
  const { data } = useQuery<AuthUser | null>({
    queryKey: AUTH_KEYS.me,
    queryFn: authService.getSession,
    staleTime: 5 * 60 * 1000,
  });
  return data ?? null;
}
