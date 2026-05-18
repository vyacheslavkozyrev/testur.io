import apiClient from '@/services/apiClient';
import type { AccountProfileDto, AccountPreferencesDto, UpdateProfileRequest, UpdatePreferencesRequest } from '@/types/account.types';

export const accountService = {
  updateProfile: (body: UpdateProfileRequest): Promise<AccountProfileDto> =>
    apiClient
      .patch<AccountProfileDto>('/v1/account/profile', body)
      .then((r) => r.data),

  getPreferences: (): Promise<AccountPreferencesDto> =>
    apiClient.get<AccountPreferencesDto>('/v1/account/preferences').then((r) => r.data),

  updatePreferences: (body: UpdatePreferencesRequest): Promise<AccountPreferencesDto> =>
    apiClient.patch<AccountPreferencesDto>('/v1/account/preferences', body).then((r) => r.data),
};
