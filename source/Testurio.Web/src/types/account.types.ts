export interface AccountProfileDto {
  userId: string;
  firstName: string | null;
  lastName: string | null;
}

export interface AccountPreferencesDto {
  language: string | null;
  theme: string | null;
}

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
}

export interface UpdatePreferencesRequest {
  language?: SupportedLanguage;
  theme?: 'light' | 'dark';
}

export type ThemeMode = 'light' | 'dark';
export type SupportedLanguage = 'en' | 'uk' | 'es' | 'be';
