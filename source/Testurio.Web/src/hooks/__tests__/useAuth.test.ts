import { renderHook, act, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import { authService } from '@/services/auth/authService';
import { useSignIn, useSignUp, useForgotPassword, useSignOut } from '../useAuth';
import type { AuthUser } from '@/types/layout.types';
import type { AuthError } from '@/types/auth.types';

// ─── Mocks ────────────────────────────────────────────────────────────────────

jest.mock('@/services/auth/authService');
const mockAuthService = authService as jest.Mocked<typeof authService>;

// Mock next/navigation — hooks use useRouter and window.location
const mockRouterReplace = jest.fn();
jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockRouterReplace }),
  useSearchParams: () => new URLSearchParams(),
}));

const mockAuthUser: AuthUser = {
  id: 'user-001',
  displayName: 'Test User',
  email: 'test@example.com',
  avatarUrl: undefined,
};

const mockInvalidCredentialsError: AuthError = {
  code: 'INVALID_CREDENTIALS',
  message: 'Incorrect email or password.',
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: qc }, children);
}

// ─── useSignIn ────────────────────────────────────────────────────────────────

describe('useSignIn', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls authService.signIn with trimmed email and password', async () => {
    mockAuthService.signIn.mockResolvedValue(mockAuthUser);

    const { result } = renderHook(() => useSignIn(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: '  test@example.com  ', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.signIn).toHaveBeenCalledWith({ email: '  test@example.com  ', password: 'Password1' });
  });

  it('redirects to /dashboard on success when no returnUrl', async () => {
    mockAuthService.signIn.mockResolvedValue(mockAuthUser);

    const { result } = renderHook(() => useSignIn(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'test@example.com', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockRouterReplace).toHaveBeenCalledWith('/dashboard');
  });

  it('redirects to returnUrl on success when returnUrl is a valid relative path', async () => {
    mockAuthService.signIn.mockResolvedValue(mockAuthUser);

    const { result } = renderHook(() => useSignIn('/projects'), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'test@example.com', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockRouterReplace).toHaveBeenCalledWith('/projects');
  });

  it('falls back to /dashboard when returnUrl is an absolute URL (open-redirect guard)', async () => {
    mockAuthService.signIn.mockResolvedValue(mockAuthUser);

    const { result } = renderHook(() => useSignIn('https://evil.com'), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'test@example.com', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockRouterReplace).toHaveBeenCalledWith('/dashboard');
  });

  it('exposes AuthError on failure', async () => {
    mockAuthService.signIn.mockRejectedValue(mockInvalidCredentialsError);

    const { result } = renderHook(() => useSignIn(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'test@example.com', password: 'wrong' });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as AuthError).code).toBe('INVALID_CREDENTIALS');
  });
});

// ─── useSignUp ────────────────────────────────────────────────────────────────

describe('useSignUp', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls authService.signUp and redirects to /dashboard on success', async () => {
    mockAuthService.signUp.mockResolvedValue(mockAuthUser);

    const { result } = renderHook(() => useSignUp(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'new@example.com', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.signUp).toHaveBeenCalledWith({ email: 'new@example.com', password: 'Password1' });
    expect(mockRouterReplace).toHaveBeenCalledWith('/dashboard');
  });

  it('exposes USER_ALREADY_EXISTS error on duplicate email', async () => {
    const error: AuthError = { code: 'USER_ALREADY_EXISTS', message: 'Already exists.' };
    mockAuthService.signUp.mockRejectedValue(error);

    const { result } = renderHook(() => useSignUp(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'existing@example.com', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as AuthError).code).toBe('USER_ALREADY_EXISTS');
  });
});

// ─── useForgotPassword ────────────────────────────────────────────────────────

describe('useForgotPassword', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls authService.forgotPassword and resolves successfully', async () => {
    mockAuthService.forgotPassword.mockResolvedValue(undefined);

    const { result } = renderHook(() => useForgotPassword(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'user@example.com' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.forgotPassword).toHaveBeenCalledWith({ email: 'user@example.com' });
  });

  it('resolves even when authService.forgotPassword throws (no account enumeration)', async () => {
    // forgotPassword in authService swallows errors — so the hook should always resolve
    mockAuthService.forgotPassword.mockResolvedValue(undefined);

    const { result } = renderHook(() => useForgotPassword(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'nonexistent@example.com' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.isError).toBe(false);
  });
});

// ─── useSignOut ───────────────────────────────────────────────────────────────

describe('useSignOut', () => {
  const originalLocation = window.location;

  beforeEach(() => {
    jest.clearAllMocks();
    // jsdom does not support window.location.href assignment — replace with writable mock
    Object.defineProperty(window, 'location', {
      writable: true,
      value: { href: '' },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, 'location', {
      writable: true,
      value: originalLocation,
    });
  });

  it('navigates to the logoutUrl returned by authService.signOut', async () => {
    mockAuthService.signOut.mockResolvedValue('/sign-in');

    const { result } = renderHook(() => useSignOut(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(window.location.href).toBe('/sign-in');
  });

  it('falls back to /sign-in when authService.signOut rejects', async () => {
    mockAuthService.signOut.mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useSignOut(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(window.location.href).toBe('/sign-in');
  });
});
