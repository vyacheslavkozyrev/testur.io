export interface AccountPreferencesDto {
  language: string | null;
  theme: string | null;
}

export interface UpdateProfileRequest {
  displayName: string;
}

export interface UpdatePreferencesRequest {
  language?: 'en' | 'uk';
  theme?: 'light' | 'dark';
}

export type ThemeMode = 'light' | 'dark';
export type SupportedLanguage = 'en' | 'uk';
