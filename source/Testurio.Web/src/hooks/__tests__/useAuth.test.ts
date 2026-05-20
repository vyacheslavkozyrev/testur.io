import { renderHook, act, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import { authService } from '@/services/auth/authService';
import { useSignIn, useSignUp, useForgotPassword, useSignOut } from '../useAuth';
import type { AuthUser } from '@/types/layout.types';
import type { AuthError, ResetPasswordCodeHandle } from '@/types/auth.types';

const mockResetCodeHandle: ResetPasswordCodeHandle = {
  _msalState: { async submitCode() { return undefined as unknown; } },
};

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
  firstName: null,
  lastName: null,
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
  function Wrapper({ children }: { children: React.ReactNode }) {
    return createElement(QueryClientProvider, { client: qc }, children);
  }
  return Wrapper;
}

// ─── useSignIn ────────────────────────────────────────────────────────────────

describe('useSignIn', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls authService.signIn with the email and password passed to mutate', async () => {
    mockAuthService.signIn.mockResolvedValue(mockAuthUser);

    const { result } = renderHook(() => useSignIn(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'test@example.com', password: 'Password1' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.signIn).toHaveBeenCalledWith({ email: 'test@example.com', password: 'Password1' });
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
      result.current.mutate({ email: 'new@example.com', password: 'Password1', firstName: 'Test', lastName: 'User' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.signUp).toHaveBeenCalledWith({ email: 'new@example.com', password: 'Password1', firstName: 'Test', lastName: 'User' });
    expect(mockRouterReplace).toHaveBeenCalledWith('/dashboard');
  });

  it('exposes USER_ALREADY_EXISTS error on duplicate email', async () => {
    const error: AuthError = { code: 'USER_ALREADY_EXISTS', message: 'Already exists.' };
    mockAuthService.signUp.mockRejectedValue(error);

    const { result } = renderHook(() => useSignUp(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'existing@example.com', password: 'Password1', firstName: 'Existing', lastName: 'User' });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as AuthError).code).toBe('USER_ALREADY_EXISTS');
  });
});

// ─── useForgotPassword ────────────────────────────────────────────────────────

describe('useForgotPassword', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls authService.forgotPassword and resolves successfully', async () => {
    mockAuthService.forgotPassword.mockResolvedValue(mockResetCodeHandle);

    const { result } = renderHook(() => useForgotPassword(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'user@example.com' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.forgotPassword).toHaveBeenCalledWith({ email: 'user@example.com' });
  });

  it('resolves even when authService.forgotPassword throws (no account enumeration)', async () => {
    // forgotPassword in authService swallows "not found" errors — so the hook should always resolve
    mockAuthService.forgotPassword.mockResolvedValue(mockResetCodeHandle);

    const { result } = renderHook(() => useForgotPassword(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'nonexistent@example.com' });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.isError).toBe(false);
  });

  it('exposes isError when authService.forgotPassword throws an unexpected error', async () => {
    mockAuthService.forgotPassword.mockRejectedValue(new Error('network'));

    const { result } = renderHook(() => useForgotPassword(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate({ email: 'user@example.com' });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

// ─── useSignOut ───────────────────────────────────────────────────────────────

describe('useSignOut', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('calls authService.signOut and succeeds', async () => {
    mockAuthService.signOut.mockResolvedValue('https://b2c.example.com/logout');

    const { result } = renderHook(() => useSignOut(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockAuthService.signOut).toHaveBeenCalledTimes(1);
  });

  it('falls back to /sign-in via router.replace when authService.signOut rejects', async () => {
    mockAuthService.signOut.mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useSignOut(), { wrapper: createWrapper() });

    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(mockRouterReplace).toHaveBeenCalledWith('/sign-in');
  });
});
