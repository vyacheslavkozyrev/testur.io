'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { authService } from '@/services/auth/authService';
import type { AuthUser } from '@/types/layout.types';
import type { AuthError, SignInRequest, SignUpRequest, ForgotPasswordRequest } from '@/types/auth.types';
import type { ApiError } from '@/types/api.types';
import { DASHBOARD_ROUTE, SIGN_IN_ROUTE } from '@/routes/routes';

/** React Query key used by `useAuthUser` — invalidated on sign-in / sign-up / sign-out. */
export const AUTH_KEYS = {
  me: ['auth', 'me'] as const,
};

/**
 * Returns a safe redirect path from `returnUrl`.
 * Rejects absolute URLs and protocol-relative URLs to prevent open-redirect attacks.
 * Only allows relative paths that start with '/' but not '//'.
 */
function getSafeReturnUrl(returnUrl?: string): string {
  if (!returnUrl) return DASHBOARD_ROUTE;
  try {
    // If `returnUrl` parses as a valid URL it is absolute — reject it.
    new URL(returnUrl);
    return DASHBOARD_ROUTE;
  } catch {
    // Not a valid absolute URL — check it's a safe relative path.
    return returnUrl.startsWith('/') && !returnUrl.startsWith('//')
      ? returnUrl
      : DASHBOARD_ROUTE;
  }
}

/**
 * Mutation hook for signing in with email and password.
 *
 * On success:
 * - Invalidates the `auth.me` cache so `useAuthUser` re-fetches the new identity.
 * - Redirects to `returnUrl` query param (if present and same-origin) or `/dashboard`.
 *
 * On error: the mutation's `error` field contains an `AuthError` the UI can render.
 */
export function useSignIn(returnUrl?: string) {
  const qc = useQueryClient();
  const router = useRouter();

  return useMutation<AuthUser, AuthError | ApiError, SignInRequest>({
    mutationFn: (req) => authService.signIn(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: AUTH_KEYS.me });
      router.replace(getSafeReturnUrl(returnUrl));
    },
  });
}

/**
 * Mutation hook for registering a new account with email and password.
 *
 * On success:
 * - B2C auto-signs the user in via continuation token.
 * - Invalidates the `auth.me` cache.
 * - Redirects to `/dashboard`.
 *
 * On error: `error.code` is one of `USER_ALREADY_EXISTS`, `INVALID_PASSWORD`, `UNKNOWN`.
 */
export function useSignUp() {
  const qc = useQueryClient();
  const router = useRouter();

  return useMutation<AuthUser, AuthError | ApiError, SignUpRequest>({
    mutationFn: (req) => authService.signUp(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: AUTH_KEYS.me });
      router.replace(DASHBOARD_ROUTE);
    },
  });
}

/**
 * Mutation hook for initiating the forgot-password flow.
 *
 * Resolves on success or on "account not found" errors (prevents enumeration).
 * Rejects with an error for unexpected failures, allowing `isError` to be exposed.
 */
export function useSubmitSignUpCode() {
  const qc = useQueryClient();
  const router = useRouter();

  return useMutation<AuthUser, AuthError | ApiError, string>({
    mutationFn: (code) => authService.submitSignUpCode(code),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: AUTH_KEYS.me });
      router.replace(DASHBOARD_ROUTE);
    },
  });
}

export function useForgotPassword() {
  return useMutation<void, Error, ForgotPasswordRequest>({
    mutationFn: (req) => authService.forgotPassword(req),
  });
}

/**
 * Mutation hook for signing out.
 *
 * On success:
 * - Clears the `auth.me` cache entry.
 * - Navigates to the B2C logout URL returned by `POST /api/auth/sign-out`.
 *
 * On error: falls back to redirecting to `/sign-in` directly so the user
 * is never left stranded on an authenticated page.
 */
export function useSignOut() {
  const qc = useQueryClient();
  const router = useRouter();

  return useMutation<string, AuthError | ApiError, void>({
    mutationFn: () => authService.signOut(),
    onSuccess: (logoutUrl) => {
      qc.removeQueries({ queryKey: AUTH_KEYS.me });
      window.location.href = logoutUrl;
    },
    onError: () => {
      qc.removeQueries({ queryKey: AUTH_KEYS.me });
      router.replace(SIGN_IN_ROUTE);
    },
  });
}
