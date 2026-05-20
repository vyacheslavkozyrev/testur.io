import { render, screen, waitFor } from '@testing-library/react';
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
  firstName: 'Test',
  lastName: 'User',
};

function createWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  function Wrapper({ children }: { children: React.ReactNode }) {
    return createElement(
      QueryClientProvider,
      { client: qc },
      createElement(
        I18nextProvider,
        { i18n: i18nInstance },
        createElement(ThemeProvider, { theme }, children),
      ),
    );
  }
  return Wrapper;
}

describe('PersonalInfoSection', () => {
  beforeEach(() => jest.clearAllMocks());

  it('pre-populates first name and last name fields from the user prop', () => {
    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const firstNameInput = screen.getByRole('textbox', { name: /first name/i }) as HTMLInputElement;
    const lastNameInput = screen.getByRole('textbox', { name: /last name/i }) as HTMLInputElement;
    expect(firstNameInput.value).toBe('Test');
    expect(lastNameInput.value).toBe('User');
  });

  it('pre-populates empty strings when user has null names', () => {
    render(
      <PersonalInfoSection user={{ ...mockUser, firstName: null, lastName: null }} onSaveSuccess={jest.fn()} />,
      { wrapper: createWrapper() },
    );
    const firstNameInput = screen.getByRole('textbox', { name: /first name/i }) as HTMLInputElement;
    expect(firstNameInput.value).toBe('');
  });

  it('disables inputs and button while request is in flight', async () => {
    let resolveUpdate: (value: { userId: string; firstName: string | null; lastName: string | null }) => void;
    const pending = new Promise<{ userId: string; firstName: string | null; lastName: string | null }>((res) => {
      resolveUpdate = res;
    });
    mockAccountService.updateProfile.mockReturnValue(pending);

    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    const saveBtn = screen.getByRole('button', { name: /save/i });
    await userEvent.click(saveBtn);

    await waitFor(() => {
      const inputs = screen.getAllByRole('textbox');
      inputs.forEach((input) => expect(input).toBeDisabled());
      expect(screen.getByRole('button')).toBeDisabled();
    });

    resolveUpdate!({ userId: 'user-1', firstName: 'Test', lastName: 'User' });
  });

  it('calls onSaveSuccess after a successful save', async () => {
    mockAccountService.updateProfile.mockResolvedValue({ userId: 'user-1', firstName: 'Test', lastName: 'User' });
    const onSaveSuccess = jest.fn();

    render(<PersonalInfoSection user={mockUser} onSaveSuccess={onSaveSuccess} />, {
      wrapper: createWrapper(),
    });
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(onSaveSuccess).toHaveBeenCalledTimes(1));
  });

  it('shows error banner when save request fails', async () => {
    mockAccountService.updateProfile.mockRejectedValue(new Error('Network error'));

    render(<PersonalInfoSection user={mockUser} onSaveSuccess={jest.fn()} />, {
      wrapper: createWrapper(),
    });
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByText(/failed to save settings/i)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: /save/i })).not.toBeDisabled());
  });
});
