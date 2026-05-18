import apiClient from '@/services/apiClient';
import type { AccountPreferencesDto, UpdateProfileRequest, UpdatePreferencesRequest } from '@/types/account.types';
import type { AuthUser } from '@/types/layout.types';

export const accountService = {
  getProfile: (): Promise<AuthUser> =>
    apiClient.get<AuthUser>('/api/auth/me').then((r) => r.data),

  updateProfile: (body: UpdateProfileRequest): Promise<{ userId: string; displayName: string | null }> =>
    apiClient
      .patch<{ userId: string; displayName: string | null }>('/v1/account/profile', body)
      .then((r) => r.data),

  getPreferences: (): Promise<AccountPreferencesDto> =>
    apiClient.get<AccountPreferencesDto>('/v1/account/preferences').then((r) => r.data),

  updatePreferences: (body: UpdatePreferencesRequest): Promise<AccountPreferencesDto> =>
    apiClient.patch<AccountPreferencesDto>('/v1/account/preferences', body).then((r) => r.data),
};
