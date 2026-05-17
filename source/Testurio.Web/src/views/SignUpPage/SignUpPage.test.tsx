import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: jest.fn() }),
}));

// ─── Mock useSignUp hook ──────────────────────────────────────────────────────

const mockMutate = jest.fn();
const mockSignUpState = {
  mutate: mockMutate,
  isPending: false,
  isError: false,
  isSuccess: false,
  error: null as null | { code: string; message: string },
};

jest.mock('@/hooks/useAuth', () => ({
  useSignUp: () => mockSignUpState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      auth: {
        signUp: {
          title: 'Create your account',
          subtitle: 'Start automating your tests in minutes',
          emailLabel: 'Email',
          emailRequired: 'Email is required',
          passwordLabel: 'Password',
          passwordRequired: 'Password is required',
          passwordMinLength: 'Password must be at least 8 characters',
          passwordUppercase: 'Password must contain at least one uppercase letter',
          passwordLowercase: 'Password must contain at least one lowercase letter',
          passwordDigit: 'Password must contain at least one digit',
          confirmPasswordLabel: 'Confirm Password',
          confirmPasswordRequired: 'Please confirm your password',
          passwordMismatch: 'Passwords do not match',
          submitButton: 'Create Account',
          signingUp: 'Creating account…',
          hasAccount: 'Already have an account?',
          signIn: 'Sign in',
          errorUserExists: 'An account with this email already exists.',
          signInInstead: 'Sign in instead?',
          errorInvalidPassword: 'Password does not meet the requirements.',
          errorGeneric: 'Something went wrong. Please try again.',
        },
      },
    },
  },
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

function renderSignUpPage() {
  const { default: SignUpPage } = jest.requireActual('./SignUpPage');
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={createTheme()}>
        <SignUpPage />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SignUpPage', () => {
  beforeEach(() => {
    mockSignUpState.isPending = false;
    mockSignUpState.isError = false;
    mockSignUpState.isSuccess = false;
    mockSignUpState.error = null;
    jest.clearAllMocks();
  });

  it('renders email, password, confirm password fields and submit button', () => {
    renderSignUpPage();
    expect(screen.getByLabelText(/Email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Password/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Confirm Password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Create Account/i })).toBeInTheDocument();
  });

  it('renders the sign-in link', () => {
    renderSignUpPage();
    expect(screen.getByText('Sign in')).toBeInTheDocument();
  });

  it('calls mutate with email and password on valid submission', async () => {
    renderSignUpPage();

    fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'new@example.com' } });
    fireEvent.change(screen.getByLabelText(/^Password/i), { target: { value: 'Password1' } });
    fireEvent.change(screen.getByLabelText(/Confirm Password/i), { target: { value: 'Password1' } });
    fireEvent.click(screen.getByRole('button', { name: /Create Account/i }));

    await waitFor(() => {
      expect(mockMutate).toHaveBeenCalledWith({ email: 'new@example.com', password: 'Password1' });
    });
  });

  it('shows password mismatch error when confirm password does not match', async () => {
    renderSignUpPage();

    fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'new@example.com' } });
    fireEvent.change(screen.getByLabelText(/^Password/i), { target: { value: 'Password1' } });
    fireEvent.change(screen.getByLabelText(/Confirm Password/i), { target: { value: 'Different1' } });
    fireEvent.click(screen.getByRole('button', { name: /Create Account/i }));

    await waitFor(() => {
      expect(screen.getByText('Passwords do not match')).toBeInTheDocument();
    });
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('shows password too short error for passwords under 8 characters', async () => {
    renderSignUpPage();

    fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'new@example.com' } });
    fireEvent.change(screen.getByLabelText(/^Password/i), { target: { value: 'Ab1' } });
    fireEvent.change(screen.getByLabelText(/Confirm Password/i), { target: { value: 'Ab1' } });
    fireEvent.click(screen.getByRole('button', { name: /Create Account/i }));

    await waitFor(() => {
      expect(screen.getByText('Password must be at least 8 characters')).toBeInTheDocument();
    });
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('shows uppercase required error when password has no uppercase letter', async () => {
    renderSignUpPage();

    fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'new@example.com' } });
    fireEvent.change(screen.getByLabelText(/^Password/i), { target: { value: 'password1' } });
    fireEvent.change(screen.getByLabelText(/Confirm Password/i), { target: { value: 'password1' } });
    fireEvent.click(screen.getByRole('button', { name: /Create Account/i }));

    await waitFor(() => {
      expect(screen.getByText('Password must contain at least one uppercase letter')).toBeInTheDocument();
    });
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('disables submit button while sign-up is in progress', () => {
    mockSignUpState.isPending = true;
    renderSignUpPage();
    expect(screen.getByRole('button', { name: /Creating account/i })).toBeDisabled();
  });

  it('shows duplicate email error with sign-in link', () => {
    mockSignUpState.isError = true;
    mockSignUpState.error = { code: 'USER_ALREADY_EXISTS', message: 'Already exists.' };
    renderSignUpPage();
    expect(screen.getByText('An account with this email already exists.')).toBeInTheDocument();
    expect(screen.getByText('Sign in instead?')).toBeInTheDocument();
  });

  it('shows invalid password error from B2C', () => {
    mockSignUpState.isError = true;
    mockSignUpState.error = { code: 'INVALID_PASSWORD', message: 'Invalid.' };
    renderSignUpPage();
    expect(screen.getByText('Password does not meet the requirements.')).toBeInTheDocument();
  });

  it('shows generic error for unknown codes', () => {
    mockSignUpState.isError = true;
    mockSignUpState.error = { code: 'UNKNOWN', message: 'Unknown error.' };
    renderSignUpPage();
    expect(screen.getByText('Something went wrong. Please try again.')).toBeInTheDocument();
  });
});
