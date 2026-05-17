import type { Configuration } from '@azure/msal-browser';

/**
 * Azure AD B2C tenant and user-flow constants.
 * Values are read from environment variables injected at build time (NEXT_PUBLIC_*).
 */
export const b2cPolicies = {
  /**
   * The combined sign-up / sign-in user flow (B2C_1_susi or equivalent).
   * Used for both new registrations and returning sign-ins.
   */
  signUpSignIn: {
    name: 'B2C_1_susi',
    authority: process.env.NEXT_PUBLIC_B2C_AUTHORITY ?? '',
  },
} as const;

/**
 * MSAL PublicClientApplication configuration.
 * Passed directly to `new PublicClientApplication(msalConfig)` in `authService`.
 */
export const msalConfig: Configuration = {
  auth: {
    clientId: process.env.NEXT_PUBLIC_B2C_CLIENT_ID ?? '',
    authority: b2cPolicies.signUpSignIn.authority,
    knownAuthorities: [process.env.NEXT_PUBLIC_B2C_KNOWN_AUTHORITY ?? ''],
    redirectUri: process.env.NEXT_PUBLIC_B2C_REDIRECT_URI ?? '/',
    postLogoutRedirectUri: '/',
    navigateToLoginRequestUrl: false,
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
};

/**
 * OAuth 2.0 scopes to include in every token request.
 * Parsed from the space-separated NEXT_PUBLIC_B2C_SCOPES env var.
 */
export const loginScopes: string[] = (
  process.env.NEXT_PUBLIC_B2C_SCOPES ?? 'openid profile email'
).split(' ').filter(Boolean);

/**
 * Minimal interface for the CustomAuthPublicClientApplication configuration.
 * Mirrors only the fields consumed by the authService — avoids importing from
 * internal dist paths of @azure/msal-browser.
 */
interface CustomAuthConfig extends Configuration {
  customAuth: {
    authApiProxyUrl: string;
    challengeTypes: string[];
  };
}

// Guard: fail fast at module load time if the native auth URL is not configured.
// Skipped in test environments where B2C is not available.
if (
  typeof process !== 'undefined' &&
  process.env.NODE_ENV !== 'test' &&
  !process.env.NEXT_PUBLIC_B2C_NATIVE_AUTH_URL
) {
  throw new Error(
    '[msalConfig] NEXT_PUBLIC_B2C_NATIVE_AUTH_URL is required but not set. ' +
    'Set it to the Entra External ID (CIAM) native authentication proxy URL.',
  );
}

/**
 * Configuration for `CustomAuthPublicClientApplication` (MSAL Native Auth).
 * Requires an Entra External ID (CIAM) tenant with Native Authentication enabled.
 */
export const customAuthConfig: CustomAuthConfig = {
  ...msalConfig,
  customAuth: {
    authApiProxyUrl: process.env.NEXT_PUBLIC_B2C_NATIVE_AUTH_URL ?? '',
    challengeTypes: ['password', 'oob'],
  },
};
