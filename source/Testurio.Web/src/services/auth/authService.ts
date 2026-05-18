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

import axios from 'axios';
import { CustomAuthPublicClientApplication } from '@azure/msal-browser/custom-auth';
import { customAuthConfig, loginScopes } from '@/config/msalConfig';
import type { AuthUser } from '@/types/layout.types';

// Separate client for Next.js API routes — uses relative URLs, not the .NET backend base URL
const nextApiClient = axios.create({ headers: { 'Content-Type': 'application/json' } });
import type { AuthError, SignInRequest, SignUpRequest, ForgotPasswordRequest } from '@/types/auth.types';

// ─── Singleton MSAL instance ──────────────────────────────────────────────────

let _msalClient: CustomAuthPublicClientApplication | null = null;

// Holds the MSAL state object between the sign-up initiation and code submission steps.
let _signUpCodeState: { submitCode(code: string): Promise<unknown> } | null = null;

// Holds first/last name provided at sign-up so they survive the OTP verification step.
let _signUpNames: { firstName: string; lastName: string } | null = null;

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

// ─── Internal helpers ─────────────────────────────────────────────────────────

/**
 * Called after sign-up completes to auto-sign the user in using the B2C continuation token.
 */
async function signInFromContinuation(
  continuationState: { signIn(inputs?: { scopes?: string[] }): Promise<import('@azure/msal-browser').SignInResult> },
  overrides?: { firstName?: string | null; lastName?: string | null },
): Promise<AuthUser> {
  let signInResult: Awaited<ReturnType<typeof continuationState.signIn>>;
  try {
    signInResult = await continuationState.signIn({ scopes: loginScopes });
  } catch (err) {
    throw makeAuthError('UNKNOWN', err instanceof Error ? err.message : 'Sign-in continuation failed.');
  }

  if (!signInResult.isCompleted()) {
    throw makeAuthError('UNKNOWN', 'Auto sign-in after registration did not complete.');
  }

  // resultData is exposed as `.data` in the MSAL native auth SignInResult at runtime
  const resultData = (signInResult as unknown as Record<string, unknown>).data as Record<string, unknown> | undefined
    ?? (signInResult as unknown as Record<string, unknown>).resultData as Record<string, unknown> | undefined;

  const account = resultData?.account as {
    localAccountId?: string;
    username?: string;
    name?: string;
    idTokenClaims?: { oid?: string; sub?: string; email?: string; emails?: string[]; name?: string; given_name?: string; family_name?: string };
  } | undefined;

  const claims = account?.idTokenClaims;
  const oid = claims?.oid ?? claims?.sub ?? account?.localAccountId;
  const email = claims?.email ?? claims?.emails?.[0] ?? account?.username ?? '';
  // Prefer form-provided values (overrides) over token claims — CIAM only returns
  // given_name/family_name in the token when they are enabled as Application claims.
  const firstName = overrides?.firstName ?? claims?.given_name ?? null;
  const lastName = overrides?.lastName ?? claims?.family_name ?? null;
  const name = claims?.name
    ?? ([firstName, lastName].filter(Boolean).join(' ') || undefined)
    ?? account?.name;

  if (!oid) throw makeAuthError('UNKNOWN', 'No user identifier received after sign-in.');

  const { data } = await nextApiClient.post<AuthUser>('/api/auth/session', { nativeClaims: { oid, email, firstName, lastName, name } });
  return data;
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
    let signInResult: Awaited<ReturnType<typeof client.signIn>>;
    try {
      signInResult = await client.signIn({ username: email, scopes: loginScopes });
    } catch (err) {
      throw makeAuthError('UNKNOWN', err instanceof Error ? err.message : 'Sign-in failed.');
    }

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

    // Step 3: Exchange credentials for a server-side session cookie
    const rawResult = (passwordResult as unknown as Record<string, unknown>).data as Record<string, unknown> | undefined
      ?? (passwordResult as unknown as Record<string, unknown>).resultData as Record<string, unknown> | undefined;
    const account = rawResult?.account as {
      localAccountId?: string; username?: string; name?: string;
      idTokenClaims?: { oid?: string; sub?: string; email?: string; emails?: string[]; name?: string; given_name?: string; family_name?: string };
    } | undefined;

    const claims = account?.idTokenClaims;
    const oid = claims?.oid ?? claims?.sub ?? account?.localAccountId;
    const userEmail = claims?.email ?? claims?.emails?.[0] ?? account?.username ?? email;
    const firstName = claims?.given_name ?? null;
    const lastName = claims?.family_name ?? null;
    const name = claims?.name
      ?? ([firstName, lastName].filter(Boolean).join(' ') || undefined)
      ?? account?.name;

    if (!oid) throw makeAuthError('UNKNOWN', 'No user identifier received from B2C.');

    try {
      const { data } = await nextApiClient.post<AuthUser>('/api/auth/session', { nativeClaims: { oid, email: userEmail, firstName, lastName, name } });
      return data;
    } catch (err: unknown) {
      const axiosErr = err as { response?: { status?: number } };
      if (axiosErr?.response?.status === 429) {
        throw makeAuthError('RATE_LIMITED', 'Too many sign-in attempts. Please wait and try again.');
      }
      throw err;
    }
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
  async signUp({ email, password, firstName, lastName }: SignUpRequest): Promise<AuthUser> {
    const client = await getMsalClient();

    const signUpResult = await client.signUp({
      username: email,
      password,
      attributes: { givenName: firstName, surname: lastName },
    });

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

    const nameOverrides = { firstName, lastName };

    // Step 2: CIAM requires email verification code before completing sign-up
    if (signUpResult.isCodeRequired()) {
      _signUpCodeState = signUpResult.state;
      _signUpNames = nameOverrides;
      throw makeAuthError('CODE_REQUIRED', 'Please check your email for a verification code.');
    }

    // Step 3: If B2C requires password submission as a separate step
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
      return signInFromContinuation(pwResult.state, nameOverrides);
    }

    if (!signUpResult.isCompleted()) {
      throw makeAuthError('UNKNOWN', 'Sign-up did not complete — additional steps required.');
    }

    return signInFromContinuation(signUpResult.state, nameOverrides);
  },

  /**
   * Submits the email verification code sent by CIAM during sign-up.
   * Must be called after `signUp` resolves with a `CODE_REQUIRED` error.
   */
  async submitSignUpCode(code: string): Promise<AuthUser> {
    if (!_signUpCodeState) {
      throw makeAuthError('UNKNOWN', 'No pending sign-up code state. Please restart sign-up.');
    }

    const codeResult = await (_signUpCodeState as {
      submitCode(code: string): Promise<{
        isFailed(): boolean;
        isCompleted(): boolean;
        isPasswordRequired(): boolean;
        error?: { message?: string; isInvalidCode?(): boolean };
        state: { signIn(inputs?: { scopes?: string[] }): Promise<import('@azure/msal-browser').SignInResult>; submitPassword?(pw: string): Promise<unknown> };
      }>;
    }).submitCode(code);

    if (codeResult.isFailed()) {
      const error = codeResult.error;
      if (error?.isInvalidCode?.()) {
        throw makeAuthError('INVALID_CODE', 'The verification code is incorrect or has expired.');
      }
      throw makeAuthError('UNKNOWN', error?.message ?? 'Code verification failed.');
    }

    if (codeResult.isCompleted()) {
      const names = _signUpNames;
      _signUpCodeState = null;
      _signUpNames = null;
      return signInFromContinuation(codeResult.state, names ?? undefined);
    }

    throw makeAuthError('UNKNOWN', 'Sign-up did not complete after code submission.');
  },

  /**
   * Initiates the B2C Self-Service Password Reset (SSPR) flow.
   * B2C sends a verification code to the email address.
   * This method resolves once the request is submitted; it does NOT wait for the email.
   *
   * Swallows "account not found" / "invalid username" errors to prevent account enumeration —
   * the caller always shows a generic confirmation message.
   * Re-throws unexpected errors so the hook can expose `isError` for non-enumeration failures.
   */
  async forgotPassword({ email }: ForgotPasswordRequest): Promise<void> {
    try {
      const client = await getMsalClient();
      await client.resetPassword({ username: email });
    } catch (err: unknown) {
      // Only swallow "not found" / "invalid username" errors (prevent enumeration).
      // Re-throw everything else so the UI can surface unexpected failures.
      const msalErr = err as { isUserNotFound?: () => boolean; isInvalidUsername?: () => boolean };
      if (msalErr?.isUserNotFound?.() || msalErr?.isInvalidUsername?.()) {
        return;
      }
      throw err;
    }
  },

  /**
   * Signs the user out: calls `POST /api/auth/sign-out` to clear the session cookie
   * and returns the B2C logout URL for the caller to redirect to.
   */
  async signOut(): Promise<string> {
    const { data } = await nextApiClient.post<{ logoutUrl: string }>('/api/auth/sign-out');
    _msalClient = null;
    _signUpCodeState = null;
    _signUpNames = null;
    return data.logoutUrl;
  },

  /**
   * Fetches the signed-in user identity from the server via `GET /api/auth/me`.
   * Returns `null` if no valid session exists (401 response).
   */
  async getSession(): Promise<AuthUser | null> {
    try {
      const { data } = await nextApiClient.get<AuthUser>('/api/auth/me');
      return data;
    } catch {
      return null;
    }
  },
};
