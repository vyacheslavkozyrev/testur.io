/**
 * authService — wraps MSAL Custom Auth (Native Authentication) for Azure AD B2C / Entra External ID.
 *
 * All methods are async and resolve to typed results or throw `AuthError` on failure.
 * This module is browser-only; do not import it in server components or Next.js API routes.
 *
 * Flow overview:
 *  signIn   → username → password → id token → POST /api/auth/session → AuthUser
 *  signUp   → username + password → (auto sign-in via continuation token) → id token → POST /api/auth/session → AuthUser
 *  forgotPassword → username → triggers B2C password-reset email (SSPR)
 *  signOut  → POST /api/auth/sign-out → clears cookie + returns B2C logout URL
 *  getSession → GET /api/auth/me → AuthUser | null
 */

import { CustomAuthPublicClientApplication } from '@azure/msal-browser';
import { customAuthConfig, loginScopes } from '@/config/msalConfig';
import apiClient from '@/services/apiClient';
import type { AuthUser } from '@/types/layout.types';
import type { AuthError, SignInRequest, SignUpRequest, ForgotPasswordRequest } from '@/types/auth.types';

// ─── Singleton MSAL instance ──────────────────────────────────────────────────

let _msalClient: CustomAuthPublicClientApplication | null = null;

async function getMsalClient(): Promise<CustomAuthPublicClientApplication> {
  if (!_msalClient) {
    _msalClient = await CustomAuthPublicClientApplication.create(customAuthConfig) as CustomAuthPublicClientApplication;
  }
  return _msalClient;
}

// ─── Error helpers ────────────────────────────────────────────────────────────

function makeAuthError(code: string, message: string): AuthError {
  return { code, message };
}

// ─── authService ──────────────────────────────────────────────────────────────

export const authService = {
  /**
   * Signs the user in with email and password using MSAL Native Auth.
   * On success, exchanges the B2C ID token for a server-side session cookie
   * via `POST /api/auth/session` and returns the `AuthUser` payload.
   *
   * @throws `AuthError` with code `INVALID_CREDENTIALS` when email or password is wrong.
   * @throws `AuthError` with code `USER_NOT_FOUND` when no account exists for the email.
   * @throws `AuthError` with code `UNKNOWN` for unexpected B2C errors.
   */
  async signIn({ email, password }: SignInRequest): Promise<AuthUser> {
    const client = await getMsalClient();

    // Step 1: Initiate sign-in with username
    const signInResult = await client.signIn({ username: email, scopes: loginScopes });

    if (signInResult.isFailed()) {
      const error = signInResult.error;
      if (error?.isUserNotFound() || error?.isInvalidUsername()) {
        throw makeAuthError('USER_NOT_FOUND', 'No account found for this email address.');
      }
      throw makeAuthError('UNKNOWN', error?.message ?? 'Sign-in failed.');
    }

    // Step 2: Submit password (email/password flow requires this second step)
    if (!signInResult.isPasswordRequired()) {
      throw makeAuthError('UNKNOWN', 'Unexpected sign-in state — password not requested.');
    }

    const passwordResult = await signInResult.state.submitPassword(password);

    if (passwordResult.isFailed()) {
      const error = passwordResult.error;
      if (error?.isInvalidPassword()) {
        throw makeAuthError('INVALID_CREDENTIALS', 'Incorrect email or password.');
      }
      throw makeAuthError('UNKNOWN', error?.message ?? 'Sign-in failed.');
    }

    if (!passwordResult.isCompleted()) {
      throw makeAuthError('UNKNOWN', 'Sign-in did not complete — additional steps required.');
    }

    // Step 3: Exchange the ID token for a server-side session cookie
    const accountData = passwordResult.resultData;
    const idToken = accountData?.getIdToken();
    if (!idToken) {
      throw makeAuthError('UNKNOWN', 'No ID token received from B2C.');
    }

    const { data } = await apiClient.post<AuthUser>('/api/auth/session', { idToken });
    return data;
  },

  /**
   * Registers a new account with email and password.
   * On success, signs the user in automatically (via MSAL continuation token)
   * and establishes a server-side session.
   *
   * @throws `AuthError` with code `USER_ALREADY_EXISTS` when the email is taken.
   * @throws `AuthError` with code `INVALID_PASSWORD` when the password does not satisfy B2C policy.
   * @throws `AuthError` with code `UNKNOWN` for unexpected errors.
   */
  async signUp({ email, password }: SignUpRequest): Promise<AuthUser> {
    const client = await getMsalClient();

    // Step 1: Initiate sign-up with username + password
    const signUpResult = await client.signUp({ username: email, password });

    if (signUpResult.isFailed()) {
      const error = signUpResult.error;
      if (error?.isUserAlreadyExists()) {
        throw makeAuthError('USER_ALREADY_EXISTS', 'An account with this email already exists.');
      }
      if (error?.isInvalidPassword()) {
        throw makeAuthError('INVALID_PASSWORD', 'Password does not meet the requirements.');
      }
      throw makeAuthError('UNKNOWN', error?.message ?? 'Sign-up failed.');
    }

    // Step 2: If B2C requires password submission as a separate step
    if (signUpResult.isPasswordRequired()) {
      const pwResult = await signUpResult.state.submitPassword(password);
      if (pwResult.isFailed()) {
        const error = pwResult.error;
        if (error?.isInvalidPassword()) {
          throw makeAuthError('INVALID_PASSWORD', 'Password does not meet the requirements.');
        }
        throw makeAuthError('UNKNOWN', error?.message ?? 'Sign-up password submission failed.');
      }
      if (!pwResult.isCompleted()) {
        throw makeAuthError('UNKNOWN', 'Sign-up did not complete after password submission.');
      }
      // Auto sign-in via continuation token
      return authService._signInFromContinuation(pwResult.state);
    }

    if (!signUpResult.isCompleted()) {
      throw makeAuthError('UNKNOWN', 'Sign-up did not complete — additional steps required.');
    }

    // Auto sign-in via continuation token
    return authService._signInFromContinuation(signUpResult.state);
  },

  /**
   * Initiates the B2C Self-Service Password Reset (SSPR) flow.
   * B2C sends a verification code to the email address.
   * This method resolves once the request is submitted; it does NOT wait for the email.
   * Always resolves without throwing — the caller shows a generic confirmation message
   * regardless of whether the account exists (prevents enumeration).
   */
  async forgotPassword({ email }: ForgotPasswordRequest): Promise<void> {
    try {
      const client = await getMsalClient();
      await client.resetPassword({ username: email });
      // We don't await the full flow — just trigger it and resolve.
    } catch {
      // Intentionally swallowed to prevent account enumeration.
    }
  },

  /**
   * Signs the user out: calls `POST /api/auth/sign-out` to clear the session cookie
   * and returns the B2C logout URL for the caller to redirect to.
   */
  async signOut(): Promise<string> {
    const { data } = await apiClient.post<{ logoutUrl: string }>('/api/auth/sign-out');
    return data.logoutUrl;
  },

  /**
   * Fetches the signed-in user identity from the server via `GET /api/auth/me`.
   * Returns `null` if no valid session exists (401 response).
   */
  async getSession(): Promise<AuthUser | null> {
    try {
      const { data } = await apiClient.get<AuthUser>('/api/auth/me');
      return data;
    } catch {
      return null;
    }
  },

  // ─── Internal helpers ──────────────────────────────────────────────────────

  /**
   * Called after sign-up completes to auto-sign the user in using the B2C continuation token.
   * @internal
   */
  async _signInFromContinuation(
    continuationState: { signIn(inputs?: { scopes?: string[] }): Promise<import('@azure/msal-browser').SignInResult> }
  ): Promise<AuthUser> {
    const signInResult = await continuationState.signIn({ scopes: loginScopes });

    if (!signInResult.isCompleted()) {
      throw makeAuthError('UNKNOWN', 'Auto sign-in after registration did not complete.');
    }

    const idToken = signInResult.resultData?.getIdToken();
    if (!idToken) {
      throw makeAuthError('UNKNOWN', 'No ID token received after registration.');
    }

    const { data } = await apiClient.post<AuthUser>('/api/auth/session', { idToken });
    return data;
  },
};
