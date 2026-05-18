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
import type { AuthError, SignInRequest, SignUpRequest, ForgotPasswordRequest, SignUpCodeHandle } from '@/types/auth.types';

// Separate client for Next.js API routes — uses relative URLs, not the .NET backend base URL
const nextApiClient = axios.create({ headers: { 'Content-Type': 'application/json' } });

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

// ─── Internal helpers ─────────────────────────────────────────────────────────

/**
 * Extracts the raw result data object from an MSAL native auth result.
 * MSAL exposes it as `.data` at runtime (not in the public type definitions).
 */
function extractMsalResultData(result: unknown): Record<string, unknown> | undefined {
  const r = result as Record<string, unknown>;
  return (r?.data as Record<string, unknown> | undefined)
    ?? (r?.resultData as Record<string, unknown> | undefined);
}

/** Extracts idToken string from MSAL result data (direct property or via getter). */
function extractIdToken(resultData: Record<string, unknown> | undefined): string | null {
  if (!resultData) return null;
  if (typeof resultData.idToken === 'string') return resultData.idToken;
  const getter = resultData as { getIdToken?(): string | undefined };
  return getter.getIdToken?.() ?? null;
}

interface MsalAccount {
  localAccountId?: string;
  username?: string;
  name?: string;
  idTokenClaims?: {
    oid?: string;
    sub?: string;
    email?: string;
    emails?: string[];
    name?: string;
    given_name?: string;
    family_name?: string;
  };
}

/**
 * Called after sign-up or code verification completes to auto-sign the user in
 * using the B2C continuation token.
 *
 * Always sends the MSAL ID token to POST /api/auth/session for server-side
 * JWT validation — never trusts self-reported claims alone.
 * firstName/lastName are supplementary display data sent alongside the token.
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

  const resultData = extractMsalResultData(signInResult);
  const idToken = extractIdToken(resultData);

  if (!idToken) {
    throw makeAuthError('UNKNOWN', 'No ID token received after sign-in. Cannot establish session.');
  }

  const account = resultData?.account as MsalAccount | undefined;
  const claims = account?.idTokenClaims;
  const firstName = overrides?.firstName ?? claims?.given_name ?? null;
  const lastName = overrides?.lastName ?? claims?.family_name ?? null;

  const { data } = await nextApiClient.post<AuthUser>('/api/auth/session', { idToken, firstName, lastName });
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

    const resultData = extractMsalResultData(passwordResult);
    const idToken = extractIdToken(resultData);

    if (!idToken) {
      throw makeAuthError('UNKNOWN', 'No ID token received from B2C. Cannot establish session.');
    }

    const account = resultData?.account as MsalAccount | undefined;
    const claims = account?.idTokenClaims;
    const firstName = claims?.given_name ?? null;
    const lastName = claims?.family_name ?? null;

    try {
      const { data } = await nextApiClient.post<AuthUser>('/api/auth/session', { idToken, firstName, lastName });
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
   *
   * On success (no code required), signs the user in automatically and returns AuthUser.
   *
   * When CIAM requires email verification, throws `AuthError` with code `CODE_REQUIRED`
   * and a `signUpCodeHandle` attached — pass that handle to `submitSignUpCode`.
   *
   * @throws `AuthError` with code `USER_ALREADY_EXISTS` when the email is taken.
   * @throws `AuthError` with code `INVALID_PASSWORD` when the password does not satisfy B2C policy.
   * @throws `AuthError` with code `CODE_REQUIRED` (with `signUpCodeHandle`) when email verification is needed.
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

    if (signUpResult.isCodeRequired()) {
      const err: AuthError = makeAuthError('CODE_REQUIRED', 'Please check your email for a verification code.');
      err.signUpCodeHandle = {
        _msalState: signUpResult.state as { submitCode(code: string): Promise<unknown> },
        _names: nameOverrides,
      };
      throw err;
    }

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
   * Must be called with the `signUpCodeHandle` from the `CODE_REQUIRED` AuthError.
   */
  async submitSignUpCode(code: string, handle: SignUpCodeHandle): Promise<AuthUser> {
    const codeResult = await (handle._msalState as {
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
      return signInFromContinuation(codeResult.state, handle._names);
    }

    throw makeAuthError('UNKNOWN', 'Sign-up did not complete after code submission.');
  },

  /**
   * Initiates the B2C Self-Service Password Reset (SSPR) flow.
   * Swallows "account not found" errors to prevent account enumeration.
   */
  async forgotPassword({ email }: ForgotPasswordRequest): Promise<void> {
    try {
      const client = await getMsalClient();
      await client.resetPassword({ username: email });
    } catch (err: unknown) {
      const msalErr = err as { isUserNotFound?: () => boolean; isInvalidUsername?: () => boolean };
      if (msalErr?.isUserNotFound?.() || msalErr?.isInvalidUsername?.()) {
        return;
      }
      throw err;
    }
  },

  /**
   * Signs the user out: calls `POST /api/auth/sign-out` to clear the session cookie.
   * Resets the MSAL singleton so the next sign-in starts with a clean in-memory state.
   */
  async signOut(): Promise<string> {
    const { data } = await nextApiClient.post<{ logoutUrl: string }>('/api/auth/sign-out');
    // Reset MSAL singleton — cache is in memoryStorage so there is nothing to clear
    // in sessionStorage; nulling the instance is sufficient.
    _msalClient = null;
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
