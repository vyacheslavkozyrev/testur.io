/** Payload sent to the sign-in mutation. */
export interface SignInRequest {
  email: string;
  password: string;
}

/** Payload sent to the sign-up mutation. */
export interface SignUpRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

/** Payload sent to the forgot-password mutation. */
export interface ForgotPasswordRequest {
  email: string;
}

/**
 * Opaque handle returned on CODE_REQUIRED — pass to authService.submitSignUpCode.
 * Carries the MSAL continuation state and form-supplied name overrides.
 */
export interface SignUpCodeHandle {
  readonly _msalState: { submitCode(code: string): Promise<unknown> };
  readonly _names: { firstName: string; lastName: string };
}

/** Opaque handle for the password-reset code step — pass to authService.submitResetCode. */
export interface ResetPasswordCodeHandle {
  readonly _msalState: { submitCode(code: string): Promise<unknown> };
}

/** Opaque handle for the new-password step — pass to authService.submitNewPassword. */
export interface ResetPasswordPasswordHandle {
  readonly _msalState: { submitNewPassword(password: string): Promise<unknown> };
}

/**
 * Decoded claims from an Azure AD B2C ID token.
 * Only the fields used by Testurio are declared here.
 */
export interface AuthTokenClaims {
  /** B2C Object ID — the stable user identifier. */
  oid: string;
  /** User's email address. */
  email?: string;
  /** User's display name, if set in the B2C profile. */
  name?: string;
}

/**
 * A structured auth error returned by `authService` methods.
 * Maps B2C error codes to UI-friendly string keys.
 */
export interface AuthError {
  /** Machine-readable error code from B2C (e.g. "AADB2C90118", "AADB2C99002"). */
  code: string;
  /** Human-readable message passed straight from the B2C response. */
  message: string;
  /**
   * Present only when `code === 'CODE_REQUIRED'`.
   * Pass this to `authService.submitSignUpCode` instead of storing module-level state.
   */
  signUpCodeHandle?: SignUpCodeHandle;
}
