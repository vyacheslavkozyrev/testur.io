'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { accountService } from '@/services/account/accountService';
import { authService } from '@/services/auth/authService';
import { AUTH_KEYS } from '@/hooks/useAuth';
import type { AccountProfileDto, AccountPreferencesDto, UpdateProfileRequest, UpdatePreferencesRequest } from '@/types/account.types';
import type { AuthUser } from '@/types/layout.types';
import type { ApiError } from '@/types/api.types';

export const ACCOUNT_KEYS = {
  preferences: ['account', 'preferences'] as const,
};

export function useAuthUserQuery() {
  return useQuery<AuthUser | null, ApiError>({
    queryKey: AUTH_KEYS.me,
    queryFn: authService.getSession,
    staleTime: 5 * 60 * 1000,
  });
}

export function useAccountPreferences() {
  return useQuery<AccountPreferencesDto, ApiError>({
    queryKey: ACCOUNT_KEYS.preferences,
    queryFn: accountService.getPreferences,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation<AccountProfileDto, ApiError, UpdateProfileRequest>({
    mutationFn: accountService.updateProfile,
    onSuccess: (updated) => {
      // Update the auth.me cache directly so the header name refreshes immediately.
      queryClient.setQueryData<AuthUser | null>(AUTH_KEYS.me, (old) =>
        old
          ? { ...old, firstName: updated.firstName, lastName: updated.lastName }
          : old,
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
