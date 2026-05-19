import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { createElement } from 'react';
import PersonalInfoSection from './PersonalInfoSection';
import { accountService } from '@/services/account/accountService';
import type { AuthUser } from '@/types/layout.types';
import settingsEn from '@/locales/en/settings.json';

jest.mock('@/services/account/accountService');
const mockAccountService = accountService as jest.Mocked<typeof accountService>;

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
        createElement(ThemeProvider, { theme }, children),
      ),
    );
}

describe('PersonalInfoSection', () => {
  beforeEach(() => jest.clearAllMocks());

  it('pre-populates the display name field from the user prop', () => {
    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const input = screen.getByRole('textbox');
    expect(input).toHaveValue('Test User');
  });

  it('shows required validation error when display name is empty', async () => {
    render(<PersonalInfoSection user={{ ...mockUser, displayName: null }} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);
    expect(await screen.findByText(/display name is required/i)).toBeInTheDocument();
  });

  it('shows max-length validation error when display name exceeds 100 characters', async () => {
    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const input = screen.getByRole('textbox');
    await userEvent.clear(input);
    await userEvent.type(input, 'a'.repeat(101));
    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);
    expect(await screen.findByText(/100 characters or fewer/i)).toBeInTheDocument();
  });

  it('disables input and button while request is in flight', async () => {
    let resolveUpdate: (value: { userId: string; displayName: string | null }) => void;
    const pending = new Promise<{ userId: string; displayName: string | null }>((res) => {
      resolveUpdate = res;
    });
    mockAccountService.updateProfile.mockReturnValue(pending);

    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);

    await waitFor(() => {
      expect(screen.getByRole('textbox')).toBeDisabled();
      expect(screen.getByRole('button')).toBeDisabled();
    });

    resolveUpdate!({ userId: 'user-1', displayName: 'Test User' });
  });

  it('calls onSaveSuccess after a successful save', async () => {
    mockAccountService.updateProfile.mockResolvedValue({ userId: 'user-1', displayName: 'Test User' });
    const onSaveSuccess = jest.fn();

    render(<PersonalInfoSection user={mockUser} onSaveSuccess={onSaveSuccess} />, {
      wrapper: createWrapper(),
    });
    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);

    await waitFor(() => expect(onSaveSuccess).toHaveBeenCalledTimes(1));
  });

  it('shows error banner when save request fails', async () => {
    mockAccountService.updateProfile.mockRejectedValue(new Error('Network error'));

    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);

    expect(await screen.findByText(/failed to save settings/i)).toBeInTheDocument();
    // Button should be re-enabled
    await waitFor(() => expect(screen.getByRole('button', { name: /save/i })).not.toBeDisabled());
  });
});
