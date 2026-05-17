# User Stories — Registration & Sign-In (0013)

## Out of Scope

The following are explicitly **not** part of this feature:

- Social login (Google, GitHub, Microsoft) — deferred to post-MVP; email/password only at MVP
- Email verification on registration — portal access is granted immediately on account creation
- Multi-user / team accounts — single-user only at v1
- Account Settings page content (display name, language preference) — covered by feature 0014
- Plan purchase / subscription activation — covered by feature 0015
- Azure AD B2C hosted UI pages — a fully custom Next.js UI is used; B2C is called via its REST API flows directly from the Next.js API routes and client-side MSAL.js
- MFA enforcement — not required at MVP

---

## Stories

### US-001: New User Registration

**As a** new visitor
**I want to** create a Testurio account using my email address and a password
**So that** I can access the portal and start setting up automated testing for my projects

#### Acceptance Criteria

- [ ] AC-001: A `/sign-up` page exists and is accessible without authentication
- [ ] AC-002: The sign-up form contains three fields: **Email**, **Password**, and **Confirm Password**
- [ ] AC-003: Submitting the form with a valid email and matching passwords that satisfy the password policy creates an account in Azure AD B2C and signs the user in automatically
- [ ] AC-004: After successful registration the user is redirected to `/dashboard`
- [ ] AC-005: If the email address is already registered, the form displays an inline error: _"An account with this email already exists. Sign in instead?"_ — the phrase "Sign in instead?" is a link that navigates to `/sign-in`
- [ ] AC-006: Password must be at least 8 characters, contain at least one uppercase letter, one lowercase letter, and one digit; violations display a specific inline field-level error message
- [ ] AC-007: If **Confirm Password** does not match **Password**, an inline error is shown on the Confirm Password field: _"Passwords do not match"_
- [ ] AC-008: Submitting the form while a request is in flight disables the submit button and shows a loading indicator; the form cannot be submitted twice
- [ ] AC-009: All visible strings on the sign-up page go through `i18next` with keys under the `auth` namespace
- [ ] AC-010: The sign-up page is not wrapped in the authenticated shell layout (no header, no sidebar)

#### Edge Cases

- If the B2C registration API returns an unexpected error (not a duplicate-email conflict), display a generic error banner: _"Something went wrong. Please try again."_
- Leading/trailing whitespace in the email field is trimmed before submission

---

### US-002: Returning User Sign-In

**As a** returning user
**I want to** sign in with my email and password
**So that** I can access my projects and test results

#### Acceptance Criteria

- [ ] AC-011: A `/sign-in` page exists and is accessible without authentication
- [ ] AC-012: The sign-in form contains two fields: **Email** and **Password**, plus a **Sign In** submit button
- [ ] AC-013: Submitting valid credentials signs the user in and redirects them to `/dashboard`
- [ ] AC-014: If a `returnUrl` query parameter is present on `/sign-in` (e.g. `/sign-in?returnUrl=%2Fprojects`), the user is redirected to that URL after successful sign-in instead of `/dashboard`; `returnUrl` must be a relative path (same-origin) — absolute or cross-origin values are ignored and fall back to `/dashboard`
- [ ] AC-015: If credentials are incorrect, display an inline error: _"Incorrect email or password"_
- [ ] AC-016: Submitting the form while a request is in flight disables the submit button and shows a loading indicator
- [ ] AC-017: The sign-in page includes a **Forgot password?** link that navigates to `/forgot-password`
- [ ] AC-018: The sign-in page includes a **Create account** link that navigates to `/sign-up`
- [ ] AC-019: All visible strings on the sign-in page go through `i18next` with keys under the `auth` namespace
- [ ] AC-020: The sign-in page is not wrapped in the authenticated shell layout

#### Edge Cases

- B2C may rate-limit sign-in attempts; if a 429 response is received, display: _"Too many attempts. Please wait a moment and try again."_
- If the B2C token endpoint returns an unexpected error, display the generic error banner: _"Something went wrong. Please try again."_

---

### US-003: Password Reset

**As a** user who has forgotten their password
**I want to** reset it via a link sent to my email
**So that** I can regain access to my account without contacting support

#### Acceptance Criteria

- [ ] AC-021: A `/forgot-password` page exists and is accessible without authentication
- [ ] AC-022: The forgot-password form contains a single **Email** field and a **Send reset link** submit button
- [ ] AC-023: Submitting a valid email address triggers the Azure AD B2C password-reset flow (SSPR); the user sees a confirmation message: _"If an account exists for that email, a reset link has been sent."_; the confirmation is shown regardless of whether the email is registered (prevents account enumeration)
- [ ] AC-024: After seeing the confirmation, the user can click **Back to sign in** which navigates to `/sign-in`
- [ ] AC-025: Submitting the form while a request is in flight disables the submit button and shows a loading indicator
- [ ] AC-026: The forgot-password page is not wrapped in the authenticated shell layout
- [ ] AC-027: All visible strings on the forgot-password page go through `i18next` with keys under the `auth` namespace

#### Edge Cases

- If the B2C SSPR endpoint returns an unexpected error, display: _"Something went wrong. Please try again."_
- The reset-link email is sent by Azure AD B2C; Testurio does not send emails directly

---

### US-004: Auth Guard — Redirect Unauthenticated Users

**As a** unauthenticated user who navigates directly to a protected URL
**I want to** be redirected to sign-in and then returned to my original destination after signing in
**So that** I do not lose context about where I was trying to go

#### Acceptance Criteria

- [ ] AC-028: Any request to a route under `/(authenticated)` (e.g. `/dashboard`, `/projects`) that does not carry a valid session is redirected to `/sign-in?returnUrl=<requested-path>`
- [ ] AC-029: The `returnUrl` parameter in the redirect URL is the originally requested relative path (e.g. `/projects/abc/settings`)
- [ ] AC-030: After a successful sign-in that was triggered by the auth guard, the user is returned to the originally requested URL (matching AC-014)
- [ ] AC-031: The auth guard is implemented server-side in the Next.js `(authenticated)` layout using cookie-based session detection; it fires before any authenticated page renders

#### Edge Cases

- If the session cookie is present but expired or malformed, it is treated as absent — the user is redirected to sign-in
- The auth guard must not redirect when the request is for a Next.js API route or a static asset

---

### US-005: Sign-Out

**As a** signed-in user
**I want to** sign out of Testurio
**So that** my session is terminated and my data is protected on shared computers

#### Acceptance Criteria

- [ ] AC-032: Clicking the **Sign Out** button in the sidebar initiates an Azure AD B2C logout; the B2C session and the local session cookie are both cleared
- [ ] AC-033: After sign-out completes, the user is redirected to `/sign-in`
- [ ] AC-034: The sign-out action is wired to the existing sidebar **Sign Out** button implemented in feature 0010a; no new UI element is needed
- [ ] AC-035: While sign-out is in progress the **Sign Out** button is disabled and shows a spinner (already implemented in 0010a — verify it still works after auth wiring)
- [ ] AC-036: The `POST /api/auth/sign-out` Next.js API route clears the `testurio_session` cookie and returns a `200` response with the B2C logout URL for the client to redirect to

#### Edge Cases

- If the B2C logout redirect fails (network error), the local session cookie is still cleared; the user is redirected to `/sign-in` directly so they are not left in a broken state

---

### US-006: Session Persistence — `/api/auth/me` Route

**As a** signed-in user
**I want to** my identity (display name, email, avatar) to be available throughout the portal without re-fetching on every page
**So that** the header always shows my correct name and avatar

#### Acceptance Criteria

- [ ] AC-037: A Next.js API route `GET /api/auth/me` exists; it reads the `testurio_session` cookie, validates the B2C ID token, and returns the `AuthUser` shape (`id`, `displayName`, `email`, `avatarUrl`)
- [ ] AC-038: If no valid session exists, `GET /api/auth/me` returns `401 Unauthorized`
- [ ] AC-039: `useAuthUser` (already implemented in 0010a) calls `GET /api/auth/me`; it returns `null` on `401` without throwing
- [ ] AC-040: The authenticated shell layout guard (US-004) uses the same session cookie for server-side validation; the client-side `useAuthUser` hook is used only for rendering user identity in the header

#### Edge Cases

- If the ID token in the session cookie has expired, `GET /api/auth/me` returns `401`; the client-side hook returns `null` and the user is shown an empty header until the page is refreshed or they sign out

---

### US-007: Root Route Redirection

**As a** visitor navigating to `/`
**I want to** be automatically redirected based on my authentication state
**So that** I land in the right place without manually typing a sub-path

#### Acceptance Criteria

- [ ] AC-041: Navigating to `/` when authenticated redirects the user to `/dashboard`
- [ ] AC-042: Navigating to `/` when unauthenticated redirects the user to `/sign-in`
- [ ] AC-043: The redirect logic is already stubbed in `source/Testurio.Web/src/app/page.tsx` using `testurio_session` cookie detection; this feature replaces the stub with real session validation using the MSAL token

#### Edge Cases

- The root route must not render any visible UI — it is a pure redirect and should resolve in the server component before any HTML is sent
