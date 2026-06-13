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
