# User Stories — Real-Life E2E Test Suite for the Web Portal (0049)

## Out of Scope

The following are explicitly **not** part of this feature:

- CI/CD integration — tests remain manual-only; they are not wired into `ui-pr-check.yaml` or any other pipeline
- Mock-based Playwright tests (those live in `e2e/*.spec.ts` and are a separate concern)
- Coverage of the public/marketing area (landing page, pricing page) — covered by existing mock spec `marketing.spec.ts`
- Admin-only routes (e.g. `/v1/admin/prompt-templates`)
- Mobile viewports
- Visual regression diffing (deferred to feature 0035)

---

## Stories

### US-001: Session Reuse via Persisted Auth State

**As a** QA engineer running the real E2E suite
**I want to** authenticate once at suite startup and reuse that session across all tests
**So that** tests are fast, isolated from login flakiness, and never re-enter credentials mid-run

#### Acceptance Criteria

- [ ] AC-001: `auth.setup.ts` signs in through the Azure AD B2C hosted UI using `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` and persists `storageState` to `e2e/.auth/user.json`
- [ ] AC-002: All tests in `e2e/real/` declare a dependency on the `setup:auth` Playwright project and load `storageState` from the persisted file
- [ ] AC-003: No test in `e2e/real/` calls a sign-in flow directly; each test starts on its target page
- [ ] AC-004: Cloudflare Access service-token headers (`CF-Access-Client-Id`, `CF-Access-Client-Secret`) are injected on every browser request during the auth setup so the CF Access gate does not block the sign-in flow

---

### US-002: Deterministic Seed Data via global-setup

**As a** QA engineer running the real E2E suite
**I want to** have a known test project provisioned once before the suite starts
**So that** per-test assertions about project names and settings are predictable without manual data setup

#### Acceptance Criteria

- [ ] AC-005: `seed.setup.ts` runs as a Playwright project (`setup:seed`) that depends on `setup:auth` and executes before the `chromium` test project
- [ ] AC-006: The setup calls `POST /v1/projects` with a deterministic payload (`name: '[E2E] Seed Project'`, `productUrl: 'https://example.com'`) and writes the returned `projectId` to `e2e/.auth/seed.json`
- [ ] AC-007: If `e2e/.auth/seed.json` already exists and `GET /v1/projects/:id` returns `200`, the setup skips re-creation (idempotent)
- [ ] AC-008: All real E2E tests that reference the seed project read `projectId` from `e2e/.auth/seed.json` rather than hardcoding an ID
- [ ] AC-009: `playwright.live.config.ts` is updated to include `setup:seed` as a Playwright project with the correct dependency chain: `chromium` depends on `['setup:auth', 'setup:seed']`

---

### US-003: Sign-In and Sign-Out Flow

**As a** QA engineer validating authentication
**I want to** verify that a test user can sign in and sign out through the portal UI
**So that** regressions in the auth flow are caught before reaching staging

#### Acceptance Criteria

- [ ] AC-010: Navigating to `/sign-in` redirects the browser to the Azure AD B2C hosted UI; after entering valid credentials and submitting, the browser lands on `/dashboard`
- [ ] AC-011: The authenticated user's display name or avatar is visible in the portal shell after sign-in
- [ ] AC-012: Clicking the Sign Out action in the sidebar clears the session and redirects to `/sign-in` (or the public landing page)
- [ ] AC-013: After sign-out, navigating directly to `/dashboard` redirects back to `/sign-in` (protected route guard is active)

---

### US-004: Project List Page

**As a** QA engineer validating project management
**I want to** verify that the real project list page renders the seed project correctly
**So that** regressions in the project list API integration are caught

#### Acceptance Criteria

- [ ] AC-014: Navigating to `/projects` renders a page containing the seed project card with the name `[E2E] Seed Project`
- [ ] AC-015: The seed project card displays the product URL `https://example.com`
- [ ] AC-016: A "Create Project" button is visible at the top of the page
- [ ] AC-017: Clicking the seed project card (or its edit icon) navigates to `/projects/:seedProjectId/settings`

---

### US-005: Project Creation

**As a** QA engineer validating project creation
**I want to** verify that a new project can be created through the portal form and appears in the list
**So that** regressions in the project creation flow are caught

#### Acceptance Criteria

- [ ] AC-018: Navigating to `/projects/new` renders a project creation form with fields for project name, product URL, and testing strategy
- [ ] AC-019: Submitting the form with valid data (`name: '[E2E] Created Project'`, `productUrl: 'https://created.example.com'`, `testingStrategy: 'E2E test'`) calls `POST /v1/projects` and navigates to the new project's settings page
- [ ] AC-020: The newly created project appears in the project list at `/projects`
- [ ] AC-021: The test cleans up the created project by calling `DELETE /v1/projects/:id` via the Playwright `request` fixture at the end of the test

---

### US-006: Project Settings Page — Settings Tab

**As a** QA engineer validating project configuration
**I want to** verify that project settings can be viewed and saved on the Settings tab
**So that** regressions in project update API integration are caught

#### Acceptance Criteria

- [ ] AC-022: Navigating to `/projects/:seedProjectId/settings` renders the Settings tab by default showing fields for project name, product URL, and testing strategy pre-populated with the seed project's values
- [ ] AC-023: Updating the testing strategy field and clicking Save calls `PUT /v1/projects/:id` with the updated value and displays a success notification
- [ ] AC-024: The custom prompt field is visible on the Settings tab and accepts text input
- [ ] AC-025: The report format & attachments section is visible on the Settings tab

---

### US-007: Project Settings Page — Integration Tab

**As a** QA engineer validating PM tool integration settings
**I want to** verify that the Integration tab is accessible and renders its form
**So that** regressions in the integration settings UI are caught

#### Acceptance Criteria

- [ ] AC-026: Clicking the Integration tab on `/projects/:seedProjectId/settings` renders the PM tool integration form without a page error
- [ ] AC-027: The Integration tab form contains fields for PM tool type selection (Azure DevOps / Jira)
- [ ] AC-028: Switching between the Settings and Integration tabs does not reload the page (client-side tab switch)

---

### US-008: Per-Project Test History — Empty State

**As a** QA engineer validating the test history page
**I want to** verify that the history page renders a meaningful empty state when the seed project has no runs
**So that** regressions in the history empty state UI are caught

#### Acceptance Criteria

- [ ] AC-029: Navigating to `/projects/:seedProjectId/history` renders the test history page without an error
- [ ] AC-030: When the seed project has no test runs, the page shows an empty state message (e.g. "No test runs yet")
- [ ] AC-031: The empty state does not display any run cards or trend charts

---

### US-009: Sidebar Navigation

**As a** QA engineer validating portal navigation
**I want to** verify that the sidebar links navigate to the correct pages
**So that** regressions in routing or the layout shell are caught

#### Acceptance Criteria

- [ ] AC-032: Clicking the "Dashboard" sidebar link from any portal page navigates to `/dashboard`
- [ ] AC-033: Clicking the "Projects" sidebar link navigates to `/projects`
- [ ] AC-034: Clicking the "Settings" sidebar link navigates to `/settings`
- [ ] AC-035: The active link is visually highlighted (has an active/selected style) matching the current route

---

### US-010: Environment Variable Configuration

**As a** QA engineer setting up the suite locally
**I want to** supply all required credentials via `.env.test` without modifying source files
**So that** no credentials are ever committed to the repository

#### Acceptance Criteria

- [ ] AC-036: The suite reads `BASE_URL`, `TEST_USER_EMAIL`, `TEST_USER_PASSWORD`, `CF_ACCESS_CLIENT_ID`, and `CF_ACCESS_CLIENT_SECRET` exclusively from `.env.test` (loaded by `playwright.live.config.ts`)
- [ ] AC-037: `.env.test` is listed in `.gitignore` and is never committed
- [ ] AC-038: The repository contains a `.env.test.example` file (or the existing `.env.example` documents these variables) so a new engineer knows which values to supply

---

### US-011: New User Registration Flow

**As a** QA engineer validating onboarding
**I want to** verify that a brand-new user can complete the sign-up flow through the portal UI
**So that** regressions in the registration path (B2C hosted UI → API user-document creation) are caught before reaching staging

#### Acceptance Criteria

- [ ] AC-039: Navigating to `/sign-up` (or clicking "Create account" on the sign-in page) redirects the browser to the Azure AD B2C / Entra External ID hosted registration UI
- [ ] AC-040: Completing the registration form (email, password, display name) and submitting creates a new account in Azure AD B2C and redirects the browser to `/dashboard`
- [ ] AC-041: Within 5 seconds of the post-registration redirect, `GET /v1/account/me` returns `200` with a user document (confirming the fire-and-forget PATCH from B2C to the API has completed and a Cosmos `Users` document exists)
- [ ] AC-042: The newly registered user's display name or avatar is visible in the portal shell after registration
- [ ] AC-043: The test uses a unique, generated email address (e.g. `e2e+reg+<timestamp>@testur.io`) so it does not collide with the permanent test user account; the generated email is not written to `.auth/` files and is not reused across runs
- [ ] AC-044: The test does not clean up the B2C account (B2C account deletion requires admin credentials out of scope for this suite); this is documented in a test-level comment

---

### US-012: Forgot Password / Password Reset Flow

**As a** QA engineer validating account recovery
**I want to** verify that the "Forgot password" link on the sign-in page initiates the B2C password-reset flow and returns the user to the portal after completion
**So that** regressions in the B2C password-reset policy are caught before reaching staging

#### Acceptance Criteria

- [ ] AC-045: Clicking the "Forgot password" or "Reset password" link on the Azure AD B2C hosted sign-in page navigates to the B2C password-reset policy page (a distinct hosted UI page, not a portal route)
- [ ] AC-046: Entering the test user's email address and submitting the verification-code form sends a code email (the test asserts the page advances to the "Enter verification code" step without validating actual email delivery)
- [ ] AC-047: After entering a valid verification code and setting a new password, the browser is redirected back to `/dashboard` (or `/sign-in` if the policy does not auto-authenticate after reset)
- [ ] AC-048: The test uses the permanent `TEST_USER_EMAIL` / `TEST_USER_PASSWORD` env-var account; if the policy auto-authenticates after reset, the test asserts the authenticated state; if it redirects to sign-in instead, the test asserts the sign-in page is shown
- [ ] AC-049: The test is tagged `@slow` and excluded from the default `playwright.live.config.ts` run filter unless explicitly opted in, because it consumes a real B2C verification-code send

---

### US-013: Post-Suite Database Cleanup (Teardown)

**As a** QA engineer maintaining a clean test environment
**I want to** have all projects created during the suite automatically deleted at the end of the run
**So that** repeated suite executions do not accumulate stale test data in Cosmos DB

#### Acceptance Criteria

- [ ] AC-050: A `teardown.ts` Playwright global teardown file is added under `e2e/real/` and registered in `playwright.live.config.ts` as the `globalTeardown` script
- [ ] AC-051: The teardown reads `projectId` from `e2e/.auth/seed.json` (if it exists) and calls `DELETE /v1/projects/:id` using the persisted auth state from `e2e/.auth/user.json`; it logs but does not throw if the project is already gone (404 is silently skipped)
- [ ] AC-052: The teardown also queries `GET /v1/projects` and deletes any project whose name starts with `[E2E]`, catching projects created by `project-create.spec.ts` that were not cleaned up inline (e.g. due to test failure mid-run)
- [ ] AC-053: The teardown does NOT delete the Azure AD B2C test user account or its Cosmos `Users` / `UserSubscriptions` documents — only projects are removed
- [ ] AC-054: After the teardown runs, `e2e/.auth/seed.json` is deleted so the next run starts fresh and re-provisions the seed project
- [ ] AC-055: The teardown uses the Playwright `request` API (not `fetch`) authenticated via the stored `storageState`, so no additional credentials are required beyond what `auth.setup.ts` already persisted
