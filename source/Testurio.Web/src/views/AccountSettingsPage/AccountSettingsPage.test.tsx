import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { createElement } from 'react';
import AccountSettingsPage from './AccountSettingsPage';
import { accountService } from '@/services/account/accountService';
import { authService } from '@/services/auth/authService';
import { ThemeContextProvider } from '@/theme/ThemeContext';
import type { AccountPreferencesDto } from '@/types/account.types';
import type { AuthUser } from '@/types/layout.types';
import type { ApiError } from '@/types/api.types';
import settingsEn from '@/locales/en/settings.json';

jest.mock('@/services/account/accountService');
jest.mock('@/services/auth/authService');

const mockAccountService = accountService as jest.Mocked<typeof accountService>;
const mockAuthService = authService as jest.Mocked<typeof authService>;

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

const mockUser: AuthUser = {
  id: 'user-1',
  displayName: 'Test User',
  email: 'test@example.com',
  firstName: null,
  lastName: null,
};

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

describe('AccountSettingsPage', () => {
  beforeEach(() => jest.clearAllMocks());

  it('renders skeleton loading state while data is loading', () => {
    // Never-resolving promises simulate loading state
    mockAuthService.getSession.mockReturnValue(new Promise(() => {}));
    mockAccountService.getPreferences.mockReturnValue(new Promise(() => {}));

    render(<AccountSettingsPage />, { wrapper: createWrapper() });

    // MUI Skeleton elements should be visible
    const skeletons = document.querySelectorAll('[class*="MuiSkeleton"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders PersonalInfoSection and PreferencesSection after data loads', async () => {
    mockAuthService.getSession.mockResolvedValue(mockUser);
    mockAccountService.getPreferences.mockResolvedValue(mockPreferences);

    render(<AccountSettingsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText(/personal information/i)).toBeInTheDocument();
      expect(screen.getByText(/preferences/i)).toBeInTheDocument();
    });
  });

  it('shows preferences error banner when preferences fetch fails with non-404', async () => {
    mockAuthService.getSession.mockResolvedValue(mockUser);
    const serverError: ApiError = { status: 500, title: 'Internal Server Error' };
    mockAccountService.getPreferences.mockRejectedValue(serverError);

    render(<AccountSettingsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText(/failed to load preferences/i)).toBeInTheDocument();
    });
  });

  it('does not show error banner when preferences return 404', async () => {
    mockAuthService.getSession.mockResolvedValue(mockUser);
    const notFoundError: ApiError = { status: 404, title: 'Not Found' };
    mockAccountService.getPreferences.mockRejectedValue(notFoundError);

    render(<AccountSettingsPage />, { wrapper: createWrapper() });

    // Wait for both queries to settle
    await waitFor(() => {
      expect(screen.getByText(/personal information/i)).toBeInTheDocument();
    });
    expect(screen.queryByText(/failed to load preferences/i)).not.toBeInTheDocument();
  });
});
