'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { accountService } from '@/services/account/accountService';
import { AUTH_KEYS } from '@/hooks/useAuth';
import type { AccountPreferencesDto, UpdateProfileRequest, UpdatePreferencesRequest } from '@/types/account.types';
import type { AuthUser } from '@/types/layout.types';
import type { ApiError } from '@/types/api.types';

export const ACCOUNT_KEYS = {
  preferences: ['account', 'preferences'] as const,
};

export function useAccountPreferences() {
  return useQuery<AccountPreferencesDto, ApiError>({
    queryKey: ACCOUNT_KEYS.preferences,
    queryFn: accountService.getPreferences,
    staleTime: 5 * 60 * 1000,
    retry: (failureCount, error) => {
      // Do not retry on 404 — it means no preferences saved yet (expected for new users)
      if (error.status === 404) return false;
      return failureCount < 3;
    },
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation<{ userId: string; displayName: string | null }, ApiError, UpdateProfileRequest>({
    mutationFn: accountService.updateProfile,
    onSuccess: (updated) => {
      // Invalidate the auth.me cache so the header display name refreshes
      queryClient.setQueryData<AuthUser>(AUTH_KEYS.me, (prev) =>
        prev ? { ...prev, displayName: updated.displayName } : prev,
      );
    },
  });
}

export function useUpdatePreferences() {
  const queryClient = useQueryClient();
  return useMutation<AccountPreferencesDto, ApiError, UpdatePreferencesRequest>({
    mutationFn: accountService.updatePreferences,
    onSuccess: (updated) => {
      queryClient.setQueryData<AccountPreferencesDto>(ACCOUNT_KEYS.preferences, updated);
    },
  });
}
