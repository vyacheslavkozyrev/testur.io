# Implementation Plan — Registration & Sign-In (0013)

## Tasks

- [x] T001 [Config] Install `@azure/msal-browser` npm package — `source/Testurio.Web/package.json`
- [x] T002 [Config] Add B2C environment variables to `.env.local` and document them: `NEXT_PUBLIC_B2C_TENANT`, `NEXT_PUBLIC_B2C_CLIENT_ID`, `NEXT_PUBLIC_B2C_AUTHORITY`, `NEXT_PUBLIC_B2C_KNOWN_AUTHORITY`, `NEXT_PUBLIC_B2C_REDIRECT_URI`, `NEXT_PUBLIC_B2C_SCOPES` — `source/Testurio.Web/.env.local`
- [x] T003 [Config] Create MSAL configuration module that reads env vars and exports a typed `msalConfig` object and `b2cPolicies` constants — `source/Testurio.Web/src/config/msalConfig.ts`
- [x] T004 [UI] Add auth TypeScript types: `SignInRequest`, `SignUpRequest`, `ForgotPasswordRequest`, `AuthTokenClaims`, `AuthError` — `source/Testurio.Web/src/types/auth.types.ts`
- [x] T005 [UI] Implement `authService`: wraps `@azure/msal-browser` to expose `signIn(email, password)`, `signUp(email, password)`, `forgotPassword(email)`, `signOut()`, `getSession()` — `source/Testurio.Web/src/services/auth/authService.ts`
- [x] T006 [UI] Add Next.js API route `GET /api/auth/me`: reads `testurio_session` cookie, validates the B2C ID token, returns `AuthUser`; returns `401` if no valid session — `source/Testurio.Web/src/app/api/auth/me/route.ts`
- [x] T007 [UI] Add Next.js API route `POST /api/auth/sign-out`: clears the `testurio_session` cookie, returns `200` with the B2C logout URL — `source/Testurio.Web/src/app/api/auth/sign-out/route.ts`
- [x] T008 [UI] Add Next.js API route `POST /api/auth/session`: receives a B2C ID token from the client, verifies it, sets the `testurio_session` HttpOnly cookie, returns `AuthUser` — `source/Testurio.Web/src/app/api/auth/session/route.ts`
- [x] T009 [UI] Update MSW mock handler for `GET /api/auth/me` to also mock `POST /api/auth/sign-out` and `POST /api/auth/session` — `source/Testurio.Web/src/mocks/handlers/auth.ts`
- [x] T010 [UI] Add `useSignIn` React Query mutation hook — `source/Testurio.Web/src/hooks/useAuth.ts`
- [x] T011 [UI] Add `useSignUp` React Query mutation hook — `source/Testurio.Web/src/hooks/useAuth.ts`
- [x] T012 [UI] Add `useForgotPassword` React Query mutation hook — `source/Testurio.Web/src/hooks/useAuth.ts`
- [x] T013 [UI] Add `useSignOut` React Query mutation hook — `source/Testurio.Web/src/hooks/useAuth.ts`
- [x] T014 [UI] Create `SignInPage` view: email + password form, Forgot password link, Create account link, inline error handling, loading state, `returnUrl` redirect on success — `source/Testurio.Web/src/views/SignInPage/SignInPage.tsx`
- [x] T015 [UI] Create `SignUpPage` view: email + password + confirm-password form, inline validation, loading state, redirect to `/dashboard` on success — `source/Testurio.Web/src/views/SignUpPage/SignUpPage.tsx`
- [x] T016 [UI] Create `ForgotPasswordPage` view: email field, submit → confirmation message, Back to sign-in link — `source/Testurio.Web/src/views/ForgotPasswordPage/ForgotPasswordPage.tsx`
- [x] T017 [UI] Add Next.js page for `/sign-in` that renders `SignInPage`; page is outside the `(authenticated)` route group — `source/Testurio.Web/src/app/(auth)/sign-in/page.tsx`
- [x] T018 [UI] Add Next.js page for `/sign-up` that renders `SignUpPage`; page is outside the `(authenticated)` route group — `source/Testurio.Web/src/app/(auth)/sign-up/page.tsx`
- [x] T019 [UI] Add Next.js page for `/forgot-password` that renders `ForgotPasswordPage`; page is outside the `(authenticated)` route group — `source/Testurio.Web/src/app/(auth)/forgot-password/page.tsx`
- [x] T020 [UI] Add `(auth)` route group layout — a minimal wrapper with no header/sidebar, just `{children}` — `source/Testurio.Web/src/app/(auth)/layout.tsx`
- [x] T021 [UI] Add auth route constants: `SIGN_IN_ROUTE`, `SIGN_UP_ROUTE`, `FORGOT_PASSWORD_ROUTE` — `source/Testurio.Web/src/routes/routes.ts`
- [x] T022 [UI] Add auth translation keys (all strings for sign-in, sign-up, forgot-password pages) — `source/Testurio.Web/src/locales/en/auth.json`
- [x] T023 [UI] Restore the auth guard in the `(authenticated)` layout: uncomment server-side session check, redirect to `/sign-in?returnUrl=<path>` when session is missing or invalid — `source/Testurio.Web/src/app/(authenticated)/layout.tsx`
- [ ] T024 [UI] Update root `page.tsx` to use real session validation from the session cookie (replace cookie-existence stub with `getSessionUserId` helper that validates the token) — `source/Testurio.Web/src/app/page.tsx`
- [ ] T025 [UI] Wire the Sign Out sidebar button: update `AppSidebar.handleSignOut` to call `POST /api/auth/sign-out` then redirect to `/sign-in` (replaces the current direct B2C logout URL construction) — `source/Testurio.Web/src/components/AppSidebar/AppSidebar.tsx`
- [ ] T026 [Test] Unit tests for `useSignIn`, `useSignUp`, `useForgotPassword`, `useSignOut` hooks — `source/Testurio.Web/src/hooks/__tests__/useAuth.test.ts`
- [ ] T027 [Test] Component tests for `SignInPage`: renders form fields, shows inline error on wrong credentials, disables submit while loading, `returnUrl` redirect applied — `source/Testurio.Web/src/views/SignInPage/SignInPage.test.tsx`
- [ ] T028 [Test] Component tests for `SignUpPage`: renders form fields, password mismatch error, password policy error, duplicate email error, redirects to dashboard on success — `source/Testurio.Web/src/views/SignUpPage/SignUpPage.test.tsx`
- [ ] T029 [Test] Component tests for `ForgotPasswordPage`: renders email field, shows confirmation message after submit regardless of whether email exists, Back to sign-in link present — `source/Testurio.Web/src/views/ForgotPasswordPage/ForgotPasswordPage.test.tsx`
- [ ] T030 [Test] E2E tests: sign-in happy path → lands on dashboard; sign-up happy path → lands on dashboard; unauthenticated access to `/dashboard` → redirected to `/sign-in?returnUrl=/dashboard`; sign-out → redirected to `/sign-in` — `source/Testurio.Web/e2e/auth.spec.ts`

## Rationale

**This feature is entirely a frontend (Next.js) concern.** Authentication delegates to Azure AD B2C via MSAL.js; no changes are required to `Testurio.Api`, `Testurio.Core`, or any backend project. The session is established client-side (MSAL token acquisition) and then handed off to the Next.js API routes, which set an HttpOnly cookie for server-side session validation.

**Config before code (T001–T003).** The npm package must be installed before any module can import it. Environment variables and the MSAL config module must exist before `authService` can reference them. Installing the package and defining config first prevents import errors blocking all subsequent tasks.

**Types before service (T004–T005).** Auth types (`SignInRequest`, `AuthError`, etc.) must be defined before `authService` can reference them. `authService` is a leaf module with no component dependencies — it wraps MSAL's browser APIs and exposes typed async functions that the hooks will call.

**API routes before hooks and components (T006–T008).** `GET /api/auth/me`, `POST /api/auth/sign-out`, and `POST /api/auth/session` are the server-side integration points. The client-side hooks call these routes; they must exist (or be mocked) before hooks are written. Adding these three routes also replaces the stubs that `useAuthUser` and `AppSidebar` currently point to.

**MSW mock update (T009).** The existing `auth.ts` MSW handler only mocks `GET /api/auth/me`. Adding mocks for `POST /api/auth/sign-out` and `POST /api/auth/session` here, before any component test is written, ensures that component tests work without a live B2C instance.

**Hooks in one file (T010–T013).** All four mutation hooks (`useSignIn`, `useSignUp`, `useForgotPassword`, `useSignOut`) are co-located in `useAuth.ts` because they share the `authService` dependency and are always imported together. They depend on `authService` (T005) and the API routes (T006–T008) being defined first.

**View components after hooks (T014–T016).** `SignInPage`, `SignUpPage`, and `ForgotPasswordPage` call the mutation hooks; hooks must exist first. They are views — they render forms and handle local UI state; they do not own routing or layouts.

**Pages and route group after views (T017–T020).** Next.js pages are thin wrappers that import view components. The `(auth)` route group layout (T020) is created alongside the pages that belong to it; it provides a minimal shell (no header/sidebar) for the auth pages.

**Route constants alongside routes (T021).** Auth route constants (`SIGN_IN_ROUTE` etc.) are added to the existing `routes.ts` file at the same time the pages are created so that all cross-linking within auth pages (e.g. "Create account" link in `SignInPage`) can import constants rather than hardcode strings.

**Translations after components (T022).** All user-visible strings are identified once the view components are complete; adding them last avoids defining keys that are never used.

**Auth guard and root redirect wiring (T023–T024).** The authenticated layout's auth guard is deliberately re-enabled as a dedicated task (T023) — it was commented out in 0010a to allow visual testing without auth. This wiring depends on the session cookie being set correctly (T008) and the route constants being available (T021). The root `page.tsx` update (T024) replaces the cookie-existence stub with a real token validator.

**Sign-out wiring (T025).** `AppSidebar.handleSignOut` currently constructs the B2C logout URL directly (an interim placeholder from 0010a). This task replaces it with a call to `POST /api/auth/sign-out` which returns the correct logout URL and also clears the `testurio_session` cookie server-side. This task depends on T007.

**Tests last (T026–T030).** Hook tests (T026) mock `authService` directly. Component tests (T027–T029) use the MSW mock handlers updated in T009. E2E tests (T030) require the full Next.js dev server with all routes, cookies, and redirects wired up — they must be the last task.

**Cross-feature dependencies.**

- **Feature 0010a (Private Cabinet Layout)**: This feature wires the Sign Out button (T025) and restores the auth guard (T023) that 0010a stubbed. Feature 0013 depends on 0010a being complete — the `AppSidebar` component and the `(authenticated)/layout.tsx` already exist and only need updating, not creation.
- **Feature 0014 (Account Settings)**: Reads `AuthUser` fields populated by this feature's `GET /api/auth/me` route. Feature 0014 cannot be meaningfully tested until 0013 is complete.
- **Feature 0015 (Plan Purchase)**: Stripe Checkout requires an authenticated user identity; the session cookie set by this feature's `POST /api/auth/session` route provides it.
- **Feature 0012 (Marketing & Pricing Pages)**: Links from the public site to `/sign-up` and `/sign-in` must reference the routes defined in T021.
- **Feature 0010 (Dashboard)** and all other authenticated features: depend on the auth guard (T023) being correctly in place so unauthenticated access is blocked.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
