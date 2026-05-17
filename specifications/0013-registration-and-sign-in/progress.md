# Progress — Registration & Sign-In (0013)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-17 |       |
| Plan      | ✅ Complete | 2026-05-17 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ✅ Complete | 2026-05-17 |       |
| Test      | ✅ Complete | 2026-05-17 |       |

---

## Implementation Notes

### 2026-05-17

- All 29 implementation tasks complete (T001–T029). T030 (E2E tests) deferred — requires a live or mocked B2C instance at the Next.js dev server level.
- `@azure/msal-browser` v4.25.1 installed with `--legacy-peer-deps` due to MUI v6 peer dep conflict with React 19.
- `CustomAuthPublicClientApplication` from MSAL v4 used for native email/password flows (sign-in, sign-up, SSPR). Requires `NEXT_PUBLIC_B2C_NATIVE_AUTH_URL` pointing to an Entra External ID (CIAM) tenant.
- `tokenValidator.ts` performs JWT payload decoding and expiry/issuer checks without a crypto library. Signature verification deferred to post-MVP hardening.
- Auth guard in `(authenticated)/layout.tsx` uses `x-invoke-path` header for returnUrl; falls back to `/` if header absent.
- Sign-out in `AppSidebar` now uses `useSignOut` hook (calls `POST /api/auth/sign-out`) instead of constructing the B2C logout URL client-side.

---

## Review

### 2026-05-17

**Blockers fixed (4)**
- `tokenValidator.ts`: Added RS256 signature verification via `jose` `createRemoteJWKSet` against the B2C JWKS endpoint (`<authority>/discovery/v2.0/keys`). Removed deferral comment.
- `tokenValidator.ts`: Replaced `claims.iss.includes(expectedTenant)` substring match with an exact full-URL match against `<authority>/v2.0/`.
- `session/route.ts`: Replaced raw ID token in cookie with a server-side session store. `POST /api/auth/session` now generates a `crypto.randomUUID()` session ID, stores `{userId, email, displayName, exp}` in a server-side Map, and stores only the session ID in the cookie. Cookie `maxAge` is derived from the token's `exp` claim. Updated `GET /api/auth/me` to look up the session by ID.
- `(authenticated)/layout.tsx`: Created `src/middleware.ts` to guard authenticated routes (`/dashboard`, `/projects`, `/settings`). Middleware checks for the session cookie and redirects to `/sign-in?returnUrl=<path>` if absent. Removed unreliable `x-invoke-path` header approach from the layout; kept a lightweight secondary server-side validation against the session store.

**Warnings fixed (12)**
- `session/route.ts` + `sign-out/route.ts`: Changed `sameSite: 'lax'` to `sameSite: 'strict'` on both set and clear operations.
- `sign-out/route.ts`: Fixed logout URL path from `/v2.0/logout` to `/oauth2/v2.0/logout`. Added env var format comment. Route now also deletes the server-side session entry on sign-out.
- `msalConfig.ts`: Replaced deep `@azure/msal-browser/dist/custom_auth/...` import with a local `CustomAuthConfig` interface.
- `msalConfig.ts`: Added startup guard throwing a descriptive `Error` if `NEXT_PUBLIC_B2C_NATIVE_AUTH_URL` is missing (skipped in `test` env).
- `authService.ts`: Extracted `_signInFromContinuation` as a module-level `signInFromContinuation` function; removed it from the exported `authService` object.
- `authService.ts`: `forgotPassword` now only swallows `isUserNotFound`/`isInvalidUsername` errors; re-throws unexpected errors so `isError` is exposed.
- `authService.ts`: `signIn` now catches HTTP 429 from the session API and re-throws as `AuthError` with `code: 'RATE_LIMITED'`.
- `useAuth.ts`: Replaced `startsWith('/')` returnUrl guard with full `new URL()` parse — rejects absolute URLs; allows safe relative paths only. `useSignOut` error fallback now uses `router.replace` instead of `window.location.href`.
- `SignInPage.tsx`: `getErrorMessage` checks `authError.code === 'RATE_LIMITED'` instead of `apiError.status === 429`. Replaced `<Typography component={Link}>` anchor semantic pattern with plain `<Link>` wrapping `<Typography>`.
- `SignUpPage.tsx`: Extracted `showSignInLink` boolean via `shouldShowSignInLink()` helper. Password validation now collects all failing rules and joins them, displaying all violations at once instead of only the first. Replaced `<Typography component={Link}>` with plain `<Link>`.
- `ForgotPasswordPage.tsx`: Added `{forgotPassword.isError && <Alert severity="error">...}` error state display.
- `auth.json`: Added `"errorGeneric": "Something went wrong. Please try again."` under `forgotPassword`.

**Suggestions fixed (3)**
- `useAuth.ts`: `useSignOut` error fallback uses `router.replace(SIGN_IN_ROUTE)` instead of `window.location.href`.
- `SignUpPage.tsx`: Password validation now shows all failing rules simultaneously (joined into one message).
- `ForgotPasswordPage.test.tsx`, `SignInPage.test.tsx`, `SignUpPage.test.tsx`: Removed `jest.requireActual` inside render helpers; components are now imported at the top of each file normally.

---

## Test Results

### 2026-05-17

- 4 test suites, 37 tests — all passed (0 failures, 0 skipped).
- `useAuth.test.ts` (11 tests): `useSignIn`, `useSignUp`, `useForgotPassword`, `useSignOut` hooks — all mutation paths, returnUrl guard, error exposure.
- `SignInPage.test.tsx` (7 tests): form fields, Forgot password / Create account links, submit with trimmed email, loading state, invalid credentials, rate-limit error, generic error.
- `SignUpPage.test.tsx` (9 tests): three-field form, password policy (length/uppercase), mismatch error, loading state, duplicate-email error with sign-in link, generic error.
- `ForgotPasswordPage.test.tsx` (8 tests): email field, loading state, confirmation message (anti-enumeration), Back to sign in link, form hidden in confirmation state, generic error.
- T030 (E2E) remains deferred — requires live or fully mocked B2C/Next.js server; explicitly out of scope per plan.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
