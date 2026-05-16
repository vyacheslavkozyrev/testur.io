import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import AccessModeSelector from './AccessModeSelector';
import type { ProjectAccessDto } from '@/types/projectAccess.types';

// ─── Mock hooks ───────────────────────────────────────────────────────────────

const mockMutate = jest.fn();
const mockUpdateAccessState = {
  mutate: mockMutate,
  isPending: false,
  isError: false,
  isSuccess: false,
};

const mockAccessData: ProjectAccessDto = {
  projectId: 'proj-001',
  accessMode: 'ipAllowlist',
  basicAuthUser: null,
  headerTokenName: null,
};

let mockUseProjectAccessResult = {
  data: mockAccessData as ProjectAccessDto | undefined,
  isPending: false,
  isError: false,
};

jest.mock('@/hooks/useProjectAccess', () => ({
  useProjectAccess: () => mockUseProjectAccessResult,
  useUpdateProjectAccess: () => mockUpdateAccessState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      projectAccess: {
        modeLabel: 'Environment access method',
        saveButton: 'Save access configuration',
        saveSuccess: 'Access configuration saved.',
        saveError: 'Failed to save access configuration. Please try again.',
        loadError: 'Failed to load access configuration.',
        'modes.ipAllowlist.label': 'IP Allowlisting (Recommended)',
        'modes.ipAllowlist.description': 'Add the Testurio static egress IPs to your staging environment firewall.',
        'modes.ipAllowlist.setupStep1': '1. Copy the IP addresses shown above.',
        'modes.ipAllowlist.setupStep2': '2. Add them to your firewall or CDN allowlist.',
        'modes.ipAllowlist.setupStep3': '3. Done.',
        'modes.basicAuth.label': 'HTTP Basic Auth',
        'modes.basicAuth.usernameLabel': 'Username',
        'modes.basicAuth.passwordLabel': 'Password',
        'modes.basicAuth.passwordStoredHint': 'A password is stored. Enter a new value to replace it.',
        'modes.headerToken.label': 'Custom Header Token',
        'modes.headerToken.headerNameLabel': 'Header Name',
        'modes.headerToken.headerNameHint': 'Alphanumeric characters and hyphens only',
        'modes.headerToken.headerValueLabel': 'Header Value',
        'modes.headerToken.valueStoredHint': 'A value is stored. Enter a new value to replace it.',
        'validation.usernameRequired': 'Username is required.',
        'validation.passwordRequired': 'Password is required.',
        'validation.headerNameRequired': 'Header name is required.',
        'validation.headerNameInvalid': 'Header name must contain only alphanumeric characters and hyphens.',
        'validation.headerValueRequired': 'Header value is required.',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

function renderComponent() {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <AccessModeSelector projectId="proj-001" />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

beforeEach(() => {
  jest.clearAllMocks();
  mockUseProjectAccessResult = {
    data: mockAccessData,
    isPending: false,
    isError: false,
  };
  mockUpdateAccessState.isPending = false;
  mockUpdateAccessState.isError = false;
  mockUpdateAccessState.isSuccess = false;
});

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AccessModeSelector', () => {
  it('renders all three mode options', () => {
    renderComponent();
    expect(screen.getByText('IP Allowlisting (Recommended)')).toBeInTheDocument();
    expect(screen.getByText('HTTP Basic Auth')).toBeInTheDocument();
    expect(screen.getByText('Custom Header Token')).toBeInTheDocument();
  });

  it('shows IP info panel when ipAllowlist mode is selected', () => {
    renderComponent();
    expect(screen.getByText('1. Copy the IP addresses shown above.')).toBeInTheDocument();
  });

  it('shows username and password fields when basicAuth is selected', async () => {
    renderComponent();
    const basicAuthRadio = screen.getByLabelText('HTTP Basic Auth');
    await userEvent.click(basicAuthRadio);

    expect(screen.getByLabelText(/Username/i)).toBeInTheDocument();
    const passwordFields = screen.getAllByLabelText(/Password/i);
    expect(passwordFields.length).toBeGreaterThan(0);
  });

  it('password field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));

    const passwordInput = screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    );
    expect(passwordInput).toBeDefined();
    expect((passwordInput as HTMLInputElement).type).toBe('password');
  });

  it('shows header name and value fields when headerToken is selected', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Custom Header Token'));

    expect(screen.getByLabelText(/Header Name/i)).toBeInTheDocument();
    const valueFields = screen.getAllByLabelText(/Header Value/i);
    expect(valueFields.length).toBeGreaterThan(0);
  });

  it('header value field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Custom Header Token'));

    const valueInput = screen.getAllByLabelText(/Header Value/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    );
    expect(valueInput).toBeDefined();
  });

  it('hides IP panel when switching from ipAllowlist to basicAuth', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));

    expect(screen.queryByText('1. Copy the IP addresses shown above.')).not.toBeInTheDocument();
  });

  it('hides credential fields when switching back to ipAllowlist', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.click(screen.getByLabelText('IP Allowlisting (Recommended)'));

    expect(screen.queryByLabelText(/Username/i)).not.toBeInTheDocument();
  });

  it('shows validation error when saving basicAuth with empty username', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.click(screen.getByText('Save access configuration'));

    await waitFor(() => {
      expect(screen.getByText('Username is required.')).toBeInTheDocument();
    });
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('shows validation error when saving basicAuth with empty password', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.type(screen.getByLabelText(/Username/i), 'admin');
    await userEvent.click(screen.getByText('Save access configuration'));

    await waitFor(() => {
      expect(screen.getByText('Password is required.')).toBeInTheDocument();
    });
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('shows validation error for invalid header name (with spaces)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Custom Header Token'));
    await userEvent.type(screen.getByLabelText(/Header Name/i), 'X Testurio Token');
    await userEvent.type(screen.getAllByLabelText(/Header Value/i)[0], 'tok-abc');
    await userEvent.click(screen.getByText('Save access configuration'));

    await waitFor(() => {
      expect(
        screen.getByText('Header name must contain only alphanumeric characters and hyphens.'),
      ).toBeInTheDocument();
    });
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('calls mutate with correct payload when saving ipAllowlist', async () => {
    renderComponent();
    await userEvent.click(screen.getByText('Save access configuration'));

    expect(mockMutate).toHaveBeenCalledWith(
      { accessMode: 'ipAllowlist' },
      expect.any(Object),
    );
  });

  it('calls mutate with correct payload when saving basicAuth', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.type(screen.getByLabelText(/Username/i), 'admin');
    await userEvent.type(screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    )!, 's3cret');
    await userEvent.click(screen.getByText('Save access configuration'));

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({
        accessMode: 'basicAuth',
        basicAuthUser: 'admin',
        basicAuthPass: 's3cret',
      }),
      expect.any(Object),
    );
  });

  it('shows error alert when save fails', () => {
    mockUpdateAccessState.isError = true;
    renderComponent();

    expect(screen.getByText('Failed to save access configuration. Please try again.')).toBeInTheDocument();
  });

  it('shows error alert when loading fails', () => {
    mockUseProjectAccessResult = { data: undefined, isPending: false, isError: true };
    renderComponent();

    expect(screen.getByText('Failed to load access configuration.')).toBeInTheDocument();
  });
});
