import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { createElement } from 'react';
import PreferencesSection from './PreferencesSection';
import { accountService } from '@/services/account/accountService';
import { ThemeContextProvider } from '@/theme/ThemeContext';
import type { AccountPreferencesDto } from '@/types/account.types';
import settingsEn from '@/locales/en/settings.json';

jest.mock('@/services/account/accountService');
const mockAccountService = accountService as jest.Mocked<typeof accountService>;

// Mock i18n changeLanguage to track calls
jest.mock('@/i18n', () => ({
  __esModule: true,
  default: {
    changeLanguage: jest.fn().mockResolvedValue(undefined),
    language: 'en',
  },
}));

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: { en: { settings: settingsEn } },
});

const theme = createTheme();

const mockPreferences: AccountPreferencesDto = {
  language: 'en',
  theme: 'light',
};

function createWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    createElement(
      QueryClientProvider,
      { client: qc },
      createElement(
        I18nextProvider,
        { i18n: i18nInstance },
        createElement(
          ThemeProvider,
          { theme },
          createElement(ThemeContextProvider, null, children),
        ),
      ),
    );
}

describe('PreferencesSection', () => {
  beforeEach(() => jest.clearAllMocks());

  it('pre-populates language dropdown from preferences prop', () => {
    render(<PreferencesSection preferences={mockPreferences} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    // The Select component should show English
    expect(screen.getByText('English')).toBeInTheDocument();
  });

  it('defaults to browser language when preferences are null', () => {
    // jsdom sets navigator.language to 'en'; so default is 'en'
    render(<PreferencesSection preferences={null} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByText('English')).toBeInTheDocument();
  });

  it('calls updatePreferences on save with current selections', async () => {
    mockAccountService.updatePreferences.mockResolvedValue({ language: 'en', theme: 'light' });
    const onSaveSuccess = jest.fn();

    render(<PreferencesSection preferences={mockPreferences} onSaveSuccess={onSaveSuccess} />, {
      wrapper: createWrapper(),
    });

    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);

    await waitFor(() => {
      expect(mockAccountService.updatePreferences).toHaveBeenCalledWith(
        expect.objectContaining({ language: 'en', theme: 'light' }),
      );
    });
    expect(onSaveSuccess).toHaveBeenCalledTimes(1);
  });

  it('shows error banner when save fails', async () => {
    mockAccountService.updatePreferences.mockRejectedValue(new Error('Network error'));

    render(<PreferencesSection preferences={mockPreferences} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });

    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);

    expect(await screen.findByText(/failed to save settings/i)).toBeInTheDocument();
  });
});
