import React from 'react';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import ApiAuthMethodSelector, { type ApiAuthMethodSelectorHandle } from './ApiAuthMethodSelector';
import type { ProjectApiAuthDto } from '@/types/projectApiAuth.types';

// ─── Mock hooks ───────────────────────────────────────────────────────────────

const mockMutateAsync = jest.fn().mockResolvedValue({});
const mockUpdateAuthState = {
  mutateAsync: mockMutateAsync,
  isPending: false,
  isError: false,
  isSuccess: false,
};

const mockAuthNone: ProjectApiAuthDto = {
  projectId: 'proj-001',
  apiAuthMethod: 'none',
  apiAuthBearerTokenConfigured: null,
  apiAuthApiKeyName: null,
  apiAuthApiKeyPlacement: null,
  apiAuthApiKeyValueConfigured: null,
  apiAuthBasicUsername: null,
  apiAuthBasicPasswordConfigured: null,
};

let mockUseProjectApiAuthResult = {
  data: mockAuthNone as ProjectApiAuthDto | undefined,
  isPending: false,
  isError: false,
};

jest.mock('@/hooks/useProjectApiAuth', () => ({
  useProjectApiAuth: () => mockUseProjectApiAuthResult,
  useUpdateProjectApiAuth: () => mockUpdateAuthState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      projectApiAuth: {
        methodLabel: 'API Authentication Method',
        loadError: 'Failed to load API authentication settings.',
        'methods.none.label': 'None (unauthenticated)',
        'methods.bearer.label': 'Bearer Token',
        'methods.bearer.tokenLabel': 'Token',
        'methods.bearer.tokenStoredHint': 'A token is stored. Enter a new value to replace it.',
        'methods.apiKey.label': 'API Key',
        'methods.apiKey.keyNameLabel': 'Key Name',
        'methods.apiKey.keyNameHint': 'The header or query parameter name',
        'methods.apiKey.placementLabel': 'Placement',
        'methods.apiKey.placementHeader': 'Header',
        'methods.apiKey.placementQuery': 'Query Parameter',
        'methods.apiKey.placementHint': 'Where to inject the key',
        'methods.apiKey.keyValueLabel': 'Key Value',
        'methods.apiKey.keyValueStoredHint': 'A value is stored. Enter a new value to replace it.',
        'methods.basic.label': 'HTTP Basic Auth',
        'methods.basic.usernameLabel': 'Username',
        'methods.basic.passwordLabel': 'Password',
        'methods.basic.passwordStoredHint': 'A password is stored. Enter a new value to replace it.',
        'validation.tokenRequired': 'Token is required.',
        'validation.keyNameRequired': 'Key name is required.',
        'validation.keyValueRequired': 'Key value is required.',
        'validation.usernameRequired': 'Username is required.',
        'validation.passwordRequired': 'Password is required.',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

function renderComponent(ref?: React.Ref<ApiAuthMethodSelectorHandle>) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <ApiAuthMethodSelector ref={ref} projectId="proj-001" />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// Calls save() inside act() and returns the thrown error (or undefined on success).
// Using this pattern instead of expect(act(...)).rejects.toThrow() guarantees
// React state updates are flushed before subsequent assertions run.
async function trySave(ref: React.RefObject<ApiAuthMethodSelectorHandle>): Promise<Error | undefined> {
  let caught: Error | undefined;
  await act(async () => {
    try {
      await ref.current?.save();
    } catch (e) {
      caught = e as Error;
    }
  });
  return caught;
}

beforeEach(() => {
  jest.clearAllMocks();
  mockMutateAsync.mockResolvedValue({});
  mockUseProjectApiAuthResult = { data: mockAuthNone, isPending: false, isError: false };
  mockUpdateAuthState.isPending = false;
  mockUpdateAuthState.isError = false;
  mockUpdateAuthState.isSuccess = false;
});

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ApiAuthMethodSelector — UI', () => {
  it('renders all four method options', () => {
    renderComponent();
    expect(screen.getByText('None (unauthenticated)')).toBeInTheDocument();
    expect(screen.getByText('Bearer Token')).toBeInTheDocument();
    expect(screen.getByText('API Key')).toBeInTheDocument();
    expect(screen.getByText('HTTP Basic Auth')).toBeInTheDocument();
  });

  it('selecting Bearer Token shows a Token field', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    expect(screen.getByLabelText(/^Token/i)).toBeInTheDocument();
  });

  it('Token field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    const input = screen.getByLabelText(/^Token/i) as HTMLInputElement;
    expect(input.type).toBe('password');
  });

  it('selecting API Key shows Key Name, Placement, and Key Value fields', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('API Key'));
    expect(screen.getByLabelText(/Key Name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Key Value/i)).toBeInTheDocument();
  });

  it('Key Value field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('API Key'));
    const input = screen.getByLabelText(/Key Value/i) as HTMLInputElement;
    expect(input.type).toBe('password');
  });

  it('selecting HTTP Basic Auth shows Username and Password fields', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    expect(screen.getByLabelText(/Username/i)).toBeInTheDocument();
    expect(screen.getAllByLabelText(/Password/i).length).toBeGreaterThan(0);
  });

  it('Password field renders as type="password" (masked)', async () => {
    renderComponent();
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    const input = screen.getAllByLabelText(/Password/i).find(
      (el) => (el as HTMLInputElement).type === 'password',
    ) as HTMLInputElement;
    expect(input.type).toBe('password');
  });

  it('pre-fills non-secret fields when loading existing api_key config', async () => {
    mockUseProjectApiAuthResult = {
      data: {
        ...mockAuthNone,
        apiAuthMethod: 'api_key',
        apiAuthApiKeyName: 'X-Api-Key',
        apiAuthApiKeyPlacement: 'header',
        apiAuthApiKeyValueConfigured: true,
      },
      isPending: false,
      isError: false,
    };
    renderComponent();
    await waitFor(() => expect(screen.getByLabelText(/Key Name/i)).toBeInTheDocument());
    expect((screen.getByLabelText(/Key Name/i) as HTMLInputElement).value).toBe('X-Api-Key');
  });

  it('pre-fills Username when loading existing basic config', async () => {
    mockUseProjectApiAuthResult = {
      data: {
        ...mockAuthNone,
        apiAuthMethod: 'basic',
        apiAuthBasicUsername: 'api-user',
        apiAuthBasicPasswordConfigured: true,
      },
      isPending: false,
      isError: false,
    };
    renderComponent();
    await waitFor(() => expect(screen.getByLabelText(/Username/i)).toBeInTheDocument());
    expect((screen.getByLabelText(/Username/i) as HTMLInputElement).value).toBe('api-user');
  });

  it('shows placeholder "••••••••" for secret fields when a value is stored', async () => {
    mockUseProjectApiAuthResult = {
      data: {
        ...mockAuthNone,
        apiAuthMethod: 'bearer',
        apiAuthBearerTokenConfigured: true,
      },
      isPending: false,
      isError: false,
    };
    renderComponent();
    await waitFor(() => expect(screen.getByLabelText(/^Token/i)).toBeInTheDocument());
    const tokenInput = screen.getByLabelText(/^Token/i) as HTMLInputElement;
    expect(tokenInput.placeholder).toBe('••••••••');
  });

  it('shows error alert when loading fails', () => {
    mockUseProjectApiAuthResult = { data: undefined, isPending: false, isError: true };
    renderComponent();
    expect(screen.getByText('Failed to load API authentication settings.')).toBeInTheDocument();
  });
});

describe('ApiAuthMethodSelector — imperative handle: isDirty', () => {
  it('is false when no changes made', () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    expect(ref.current?.isDirty).toBe(false);
  });

  it('is true after switching to a different method', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    expect(ref.current?.isDirty).toBe(true);
  });

  it('is false after switching and switching back', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    await userEvent.click(screen.getByLabelText('None (unauthenticated)'));
    expect(ref.current?.isDirty).toBe(false);
  });
});

describe('ApiAuthMethodSelector — imperative handle: save()', () => {
  it('calls mutateAsync with none payload when method is none', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await act(async () => { await ref.current?.save(); });
    expect(mockMutateAsync).toHaveBeenCalledWith({ apiAuthMethod: 'none' });
  });

  it('calls mutateAsync with bearer payload', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    await userEvent.type(screen.getByLabelText(/^Token/i), 'my-tok');
    await act(async () => { await ref.current?.save(); });
    expect(mockMutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({ apiAuthMethod: 'bearer', apiAuthBearerToken: 'my-tok' }),
    );
  });

  it('shows validation error when bearer token is empty', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    const err = await trySave(ref);
    expect(err).toBeDefined();
    expect(mockMutateAsync).not.toHaveBeenCalled();
    expect(screen.getByText('Token is required.')).toBeInTheDocument();
  });

  it('shows validation error when basic username is empty', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('HTTP Basic Auth'));
    const err = await trySave(ref);
    expect(err).toBeDefined();
    expect(mockMutateAsync).not.toHaveBeenCalled();
    expect(screen.getByText('Username is required.')).toBeInTheDocument();
  });

  it('clears secret fields after successful save', async () => {
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await userEvent.click(screen.getByLabelText('Bearer Token'));
    const tokenInput = screen.getByLabelText(/^Token/i) as HTMLInputElement;
    await userEvent.type(tokenInput, 'tok-value');
    expect(tokenInput.value).toBe('tok-value');
    await act(async () => { await ref.current?.save(); });
    await waitFor(() => { expect(tokenInput.value).toBe(''); });
  });

  it('shows validation error when bearer method is loaded but no existing token is stored and user types nothing', async () => {
    mockUseProjectApiAuthResult = {
      data: {
        ...mockAuthNone,
        apiAuthMethod: 'bearer',
        apiAuthBearerTokenConfigured: false,
      },
      isPending: false,
      isError: false,
    };
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await waitFor(() => expect(screen.getByLabelText(/^Token/i)).toBeInTheDocument());
    const err = await trySave(ref);
    expect(err).toBeDefined();
    expect(mockMutateAsync).not.toHaveBeenCalled();
    expect(screen.getByText('Token is required.')).toBeInTheDocument();
  });

  it('propagates mutateAsync error to caller', async () => {
    mockMutateAsync.mockRejectedValue(new Error('Network error'));
    const ref = React.createRef<ApiAuthMethodSelectorHandle>();
    renderComponent(ref);
    await expect(act(async () => { await ref.current?.save(); })).rejects.toThrow('Network error');
  });
});
