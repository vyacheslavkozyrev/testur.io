import { http, HttpResponse } from 'msw';
import type { AccountProfileDto, AccountPreferencesDto } from '@/types/account.types';

const mockPreferences: AccountPreferencesDto = {
  language: 'en',
  theme: 'light',
};

const mockProfile: AccountProfileDto = {
  userId: 'mock-user-id',
  displayName: 'Test User',
};

export const accountHandlers = [
  http.patch('/v1/account/profile', async ({ request }) => {
    const body = (await request.json()) as { displayName?: string };
    return HttpResponse.json({ ...mockProfile, displayName: body.displayName ?? mockProfile.displayName });
  }),

  http.get('/v1/account/preferences', () => HttpResponse.json(mockPreferences)),

  http.patch('/v1/account/preferences', async ({ request }) => {
    const body = (await request.json()) as Partial<AccountPreferencesDto>;
    return HttpResponse.json({ ...mockPreferences, ...body });
  }),
];
