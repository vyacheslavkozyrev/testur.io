import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import ForgotPasswordPage from './ForgotPasswordPage';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: jest.fn() }),
}));

// ─── Mock useForgotPassword hook ──────────────────────────────────────────────

const mockMutate = jest.fn();
const mockForgotPasswordState = {
  mutate: mockMutate,
  isPending: false,
  isError: false,
  isSuccess: false,
  error: null,
};

jest.mock('@/hooks/useAuth', () => ({
  useForgotPassword: () => mockForgotPasswordState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      auth: {
        forgotPassword: {
          title: 'Reset your password',
          subtitle: "Enter your email and we'll send a reset link if an account exists.",
          emailLabel: 'Email',
          emailRequired: 'Email is required',
          submitButton: 'Send reset link',
          sending: 'Sending…',
          confirmationMessage: 'If an account exists for that email, a reset link has been sent.',
          backToSignIn: 'Back to sign in',
          errorGeneric: 'Something went wrong. Please try again.',
        },
      },
    },
  },
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

function renderForgotPasswordPage() {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={createTheme()}>
        <ForgotPasswordPage />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ForgotPasswordPage', () => {
  beforeEach(() => {
    mockForgotPasswordState.isPending = false;
    mockForgotPasswordState.isError = false;
    mockForgotPasswordState.isSuccess = false;
    mockForgotPasswordState.error = null;
    jest.clearAllMocks();
  });

  it('renders the email field and submit button', () => {
    renderForgotPasswordPage();
    expect(screen.getByLabelText(/Email/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Send reset link/i })).toBeInTheDocument();
  });

  it('renders the Back to sign in link in the form state', () => {
    renderForgotPasswordPage();
    expect(screen.getByText('Back to sign in')).toBeInTheDocument();
  });

  it('calls mutate with trimmed email on submission', async () => {
    renderForgotPasswordPage();

    fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'user@example.com' } });
    fireEvent.click(screen.getByRole('button', { name: /Send reset link/i }));

    await waitFor(() => {
      expect(mockMutate).toHaveBeenCalledWith({ email: 'user@example.com' });
    });
  });

  it('disables submit button while request is in flight', () => {
    mockForgotPasswordState.isPending = true;
    renderForgotPasswordPage();
    expect(screen.getByRole('button', { name: /Sending/i })).toBeDisabled();
  });

  it('shows confirmation message after successful submission', () => {
    mockForgotPasswordState.isSuccess = true;
    renderForgotPasswordPage();
    expect(
      screen.getByText('If an account exists for that email, a reset link has been sent.'),
    ).toBeInTheDocument();
  });

  it('shows Back to sign in button in confirmation state', () => {
    mockForgotPasswordState.isSuccess = true;
    renderForgotPasswordPage();
    // The confirmation state shows "Back to sign in" as a Button element
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toBeInTheDocument();
  });

  it('hides the form and shows only the confirmation after submit', () => {
    mockForgotPasswordState.isSuccess = true;
    renderForgotPasswordPage();
    // Form fields should not be visible in confirmation state
    expect(screen.queryByLabelText(/Email/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Send reset link/i })).not.toBeInTheDocument();
  });

  it('shows a generic error alert when isError is true', () => {
    mockForgotPasswordState.isError = true;
    renderForgotPasswordPage();
    expect(screen.getByText('Something went wrong. Please try again.')).toBeInTheDocument();
  });
});
