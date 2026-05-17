import React from 'react';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import AccessModeSelector, { type AccessModeSelectorHandle } from './AccessModeSelector';
import type { ProjectAccessDto } from '@/types/projectAccess.types';

// ─── Mock hooks ───────────────────────────────────────────────────────────────

const mockMutateAsync = jest.fn().mockResolvedValue({});
const mockUpdateAccessState = {
  mutateAsync: mockMutateAsync,
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

function renderComponent(ref?: React.Ref<AccessModeSelectorHandle>) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <AccessModeSelector ref={ref} projectId="proj-001" />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

beforeEach(() => {
  jest.clearAllMocks();
  mockMutateAsync.mockResolvedValue({});
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

describe('AccessModeSelector — UI', () => {
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
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    expect(screen.getByLabelText(/Username/i)).toBeInTheDocument();
    expect(screen.getAllByLabelText(/Password/i).length).toBeGreaterThan(0);
  });

  it('password field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    const passwordInput = screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    );
    expect((passwordInput as HTMLInputElement).type).toBe('password');
  });

  it('shows header name and value fields when headerToken is selected', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Custom Header Token'));
    expect(screen.getByLabelText(/Header Name/i)).toBeInTheDocument();
    expect(screen.getAllByLabelText(/Header Value/i).length).toBeGreaterThan(0);
  });

  it('header value field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Custom Header Token'));
    const valueInput = screen.getAllByLabelText(/Header Value/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    );
    expect((valueInput as HTMLInputElement).type).toBe('password');
  });

  it('hides IP panel when switching to basicAuth', async () => {
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

  it('shows error alert when loading fails', () => {
    mockUseProjectAccessResult = { data: undefined, isPending: false, isError: true };
    renderComponent();
    expect(screen.getByText('Failed to load access configuration.')).toBeInTheDocument();
  });

  it('has no standalone save button', () => {
    renderComponent();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

describe('AccessModeSelector — imperative handle: isDirty', () => {
  it('is false when no changes made (ipAllowlist, same as server)', () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    expect(ref.current?.isDirty).toBe(false);
  });

  it('is true after switching mode from server value', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    expect(ref.current?.isDirty).toBe(true);
  });

  it('is false after switching mode and switching back', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.click(screen.getByLabelText('IP Allowlisting (Recommended)'));
    expect(ref.current?.isDirty).toBe(false);
  });

  it('is true when basicAuth username differs from server value', async () => {
    mockUseProjectAccessResult = {
      data: { projectId: 'proj-001', accessMode: 'basicAuth', basicAuthUser: 'admin', headerTokenName: null },
      isPending: false,
      isError: false,
    };
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await waitFor(() => expect(screen.getByLabelText(/Username/i)).toBeInTheDocument());
    await userEvent.clear(screen.getByLabelText(/Username/i));
    await userEvent.type(screen.getByLabelText(/Username/i), 'new-user');
    expect(ref.current?.isDirty).toBe(true);
  });

  it('is true when a new password is typed (secret field)', async () => {
    mockUseProjectAccessResult = {
      data: { projectId: 'proj-001', accessMode: 'basicAuth', basicAuthUser: 'admin', headerTokenName: null },
      isPending: false,
      isError: false,
    };
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await waitFor(() => expect(screen.getAllByLabelText(/Password/i).length).toBeGreaterThan(0));
    const passwordInput = screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    )!;
    await userEvent.type(passwordInput, 'newpass');
    expect(ref.current?.isDirty).toBe(true);
  });
});

describe('AccessModeSelector — imperative handle: save()', () => {
  it('calls mutateAsync with ipAllowlist payload', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await act(async () => { await ref.current?.save(); });
    expect(mockMutateAsync).toHaveBeenCalledWith({ accessMode: 'ipAllowlist' });
  });

  it('calls mutateAsync with basicAuth payload', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.type(screen.getByLabelText(/Username/i), 'admin');
    const passwordInput = screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    )!;
    await userEvent.type(passwordInput, 's3cret');
    await act(async () => { await ref.current?.save(); });
    expect(mockMutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({ accessMode: 'basicAuth', basicAuthUser: 'admin', basicAuthPass: 's3cret' }),
    );
  });

  it('throws and shows validation error when basicAuth username is empty', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await expect(act(async () => { await ref.current?.save(); })).rejects.toThrow();
    expect(mockMutateAsync).not.toHaveBeenCalled();
    await waitFor(() => {
      expect(screen.getByText('Username is required.')).toBeInTheDocument();
    });
  });

  it('throws and shows validation error for invalid header name', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('Custom Header Token'));
    await userEvent.type(screen.getByLabelText(/Header Name/i), 'X Testurio Token');
    await userEvent.type(screen.getAllByLabelText(/Header Value/i)[0], 'tok-abc');
    await expect(act(async () => { await ref.current?.save(); })).rejects.toThrow();
    expect(mockMutateAsync).not.toHaveBeenCalled();
    await waitFor(() => {
      expect(
        screen.getByText('Header name must contain only alphanumeric characters and hyphens.'),
      ).toBeInTheDocument();
    });
  });

  it('clears secret fields after successful save', async () => {
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    await userEvent.type(screen.getByLabelText(/Username/i), 'admin');
    const passwordInput = screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    )! as HTMLInputElement;
    await userEvent.type(passwordInput, 's3cret');
    expect(passwordInput.value).toBe('s3cret');
    await act(async () => { await ref.current?.save(); });
    await waitFor(() => { expect(passwordInput.value).toBe(''); });
  });

  it('propagates mutateAsync error so caller can catch it', async () => {
    mockMutateAsync.mockRejectedValue(new Error('Network error'));
    const ref = React.createRef<AccessModeSelectorHandle>();
    renderComponent(ref);
    await expect(act(async () => { await ref.current?.save(); })).rejects.toThrow('Network error');
  });
});
