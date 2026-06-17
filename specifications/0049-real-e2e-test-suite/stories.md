# User Stories — Real-Life E2E Test Suite for the Web Portal (0049)

## Out of Scope

The following are explicitly **not** part of this feature:

- CI/CD integration — tests remain manual-only; they are not wired into `ui-pr-check.yaml` or any other pipeline
- Mock-based Playwright tests (those live in `e2e/*.spec.ts` and are a separate concern)
- Admin-only routes (e.g. `/v1/admin/prompt-templates`)
- Mobile viewports
- Visual regression diffing (deferred to feature 0035)
- Back-end pipeline stages (StoryParser, AgentRouter, Generators, Executors, ReportWriter) — no real test runs are triggered by the suite

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
- [ ] AC-004: Cloudflare Access service-token headers (`CF-Access-Client-Id`, `CF-Access-Client-Secret`) are injected on every browser request during auth setup so the CF Access gate does not block the sign-in flow

Spec ref: 0013, 0010a
Status: Pending

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
- [ ] AC-009: `playwright.live.config.ts` includes `setup:seed` as a Playwright project with the correct dependency chain: `chromium` depends on `['setup:auth', 'setup:seed']`

Spec ref: 0006
Status: Pending

---

### US-003: Sign-In — Happy Path

**As a** QA engineer validating authentication
**I want to** verify that a test user can sign in through the portal UI and land on the dashboard
**So that** regressions in the sign-in flow are caught before reaching staging

#### Acceptance Criteria

- [ ] AC-010: Navigating to `/sign-in` renders the sign-in form with Email and Password fields and a Sign In button
- [ ] AC-011: Submitting valid credentials (`TEST_USER_EMAIL`, `TEST_USER_PASSWORD`) redirects the browser to `/dashboard`
- [ ] AC-012: The authenticated user's display name or avatar is visible in the portal shell after sign-in
- [ ] AC-013: The sign-in page is not wrapped in the authenticated shell layout (no header, no sidebar visible on `/sign-in`)

#### Edge Cases

- [ ] AC-014: Navigating to `/sign-in` while already authenticated redirects to `/dashboard` (no sign-in form rendered for active sessions)

Spec ref: 0013
Status: Pending

---

### US-004: Sign-In — Wrong Password Error State

**As a** QA engineer validating authentication error handling
**I want to** verify that submitting incorrect credentials shows the appropriate inline error
**So that** regressions in credential validation feedback are caught

#### Acceptance Criteria

- [ ] AC-015: Submitting the sign-in form with a valid email and a wrong password displays an inline error message: "Incorrect email or password"
- [ ] AC-016: The Sign In button is re-enabled after the error is shown; the user can correct and resubmit
- [ ] AC-017: No redirect occurs when credentials are incorrect; the user remains on `/sign-in`

Spec ref: 0013
Status: Pending

---

### US-005: Sign-Out Flow

**As a** QA engineer validating session termination
**I want to** verify that the Sign Out action clears the session and redirects to the sign-in page
**So that** regressions in the logout flow are caught

#### Acceptance Criteria

- [ ] AC-018: Clicking the Sign Out button in the sidebar (present at the bottom of the sidebar) initiates the Azure AD B2C logout
- [ ] AC-019: After sign-out completes, the browser is redirected to `/sign-in` (or the public landing page `/`)
- [ ] AC-020: After sign-out, navigating directly to `/dashboard` redirects back to `/sign-in` (route guard is active)
- [ ] AC-021: The Sign Out button is disabled and shows a loading spinner while the logout is in progress

#### Edge Cases

- [ ] AC-022: After sign-out, pressing the browser Back button to a cached authenticated page still redirects to `/sign-in`

Spec ref: 0013, 0010a
Status: Pending

---

### US-006: Route Guard — Unauthenticated User Redirected to Sign-In

**As a** QA engineer validating access control
**I want to** verify that unauthenticated navigation to protected URLs is redirected to sign-in
**So that** regressions in the auth guard are caught

#### Acceptance Criteria

- [ ] AC-023: Accessing `/dashboard` without a valid session (e.g. using a fresh browser context with no stored auth) redirects to `/sign-in`
- [ ] AC-024: The redirect URL contains a `returnUrl` query parameter preserving the originally requested path (e.g. `/sign-in?returnUrl=%2Fdashboard`)
- [ ] AC-025: Accessing `/projects` without a valid session redirects to `/sign-in`
- [ ] AC-026: Accessing `/settings` without a valid session redirects to `/sign-in`
- [ ] AC-027: Accessing `/projects/:id/settings` without a valid session redirects to `/sign-in`

Spec ref: 0013, 0010a
Status: Pending

---

### US-007: New User Registration — Happy Path

**As a** QA engineer validating onboarding
**I want to** verify that a brand-new user can complete the sign-up flow through the portal UI
**So that** regressions in the registration path are caught before reaching staging

#### Acceptance Criteria

- [ ] AC-028: Navigating to `/sign-up` (or clicking "Create account" on the sign-in page) renders the registration form with Email, Password, and Confirm Password fields
- [ ] AC-029: Completing the registration form with a unique generated email (e.g. `e2e+reg+<timestamp>@testur.io`), a valid password, and a matching confirm-password creates an account and redirects to `/dashboard`
- [ ] AC-030: Within 5 seconds of the post-registration redirect, `GET /v1/account/me` returns `200` (confirming the Cosmos `Users` document was created)
- [ ] AC-031: The newly registered user's display name or avatar is visible in the portal shell after registration
- [ ] AC-032: The sign-up page is not wrapped in the authenticated shell layout

#### Edge Cases

- [ ] AC-033: The test uses a unique generated email per run; the email is not written to `.auth/` files and is not reused
- [ ] AC-034: The test does NOT attempt to clean up the B2C account (admin credentials out of scope); a comment documents this

Spec ref: 0013
Status: Pending

---

### US-008: Registration — Duplicate Email Error State

**As a** QA engineer validating registration error handling
**I want to** verify that attempting to register with an already-registered email shows the appropriate error
**So that** regressions in duplicate-email detection are caught

#### Acceptance Criteria

- [ ] AC-035: Submitting the registration form with the permanent test user's email (`TEST_USER_EMAIL`) and a valid password displays an inline error: "An account with this email already exists. Sign in instead?"
- [ ] AC-036: The "Sign in instead?" text is a link that navigates to `/sign-in`
- [ ] AC-037: No account is created and no redirect occurs when the error is shown

Spec ref: 0013
Status: Pending

---

### US-009: Forgot Password / Password Reset Flow

**As a** QA engineer validating account recovery
**I want to** verify that the "Forgot password?" link on the sign-in page initiates the B2C password-reset flow
**So that** regressions in the B2C password-reset policy are caught before reaching staging

#### Acceptance Criteria

- [ ] AC-038: Clicking the "Forgot password?" link on the sign-in page navigates to `/forgot-password`
- [ ] AC-039: The forgot-password page renders a single Email field and a "Send reset link" submit button
- [ ] AC-040: Submitting a valid email displays the confirmation message: "If an account exists for that email, a reset link has been sent."
- [ ] AC-041: The confirmation page includes a "Back to sign in" link that navigates to `/sign-in`
- [ ] AC-042: The confirmation is shown regardless of whether the email is registered (no account enumeration)
- [ ] AC-043: The forgot-password page is not wrapped in the authenticated shell layout

#### Edge Cases

- [ ] AC-044: Submitting with an empty email shows an inline validation error and the form is not submitted

Spec ref: 0013
Status: Pending

---

### US-010: Pricing Page Renders Plans Correctly

**As a** QA engineer validating the public marketing site
**I want to** verify that the pricing page displays all four plans with the correct billing toggle
**So that** regressions in plan display or the pricing page API integration are caught

#### Acceptance Criteria

- [ ] AC-045: Navigating to `/pricing` renders four plan cards in order: Test Junior, Test Pro, Team, Centurio
- [ ] AC-046: Each plan card displays a plan name, a price, and at least one feature item
- [ ] AC-047: A Monthly / Annual toggle is visible above the plan grid and defaults to Monthly
- [ ] AC-048: Switching to Annual updates plan card prices without a full page reload
- [ ] AC-049: The Test Pro card is visually marked as "Most popular" (badge or elevated style)
- [ ] AC-050: The pricing page is accessible without authentication

Spec ref: 0012, 0015
Status: Pending

---

### US-011: Subscription Purchase via Stripe Checkout

**As a** QA engineer validating the billing flow
**I want to** verify that an authenticated user can complete the Stripe Checkout flow and see their subscription activated
**So that** regressions in the plan purchase path are caught

#### Acceptance Criteria

- [ ] AC-051: An authenticated user on the pricing page sees "Start free trial" CTA buttons on each plan card
- [ ] AC-052: Clicking a CTA on the pricing page navigates to Stripe Checkout without an intermediate step
- [ ] AC-053: Completing Stripe Checkout with test card details redirects to `/billing/success?session_id=<id>`
- [ ] AC-054: `/billing/success` displays a confirmation message once subscription status reaches `Trialing`
- [ ] AC-055: After successful checkout, `GET /api/billing/subscription` returns `status: Trialing` within 30 seconds

Spec ref: 0015
Status: Done (`e2e/real/subscription.spec.ts`)

---

### US-012: Billing Page / Subscription Status After Purchase

**As a** QA engineer validating subscription management UI
**I want to** verify that the subscription section in Account Settings shows the correct plan details after purchase
**So that** regressions in the billing status display are caught

#### Acceptance Criteria

- [ ] AC-056: Navigating to `/settings` (or the billing tab within settings) while on a `Trialing` or `Active` subscription displays the subscription section with plan name, billing interval, and current period end date
- [ ] AC-057: A "Manage billing" button is visible for users with `Active`, `Trialing`, or `CancelledPendingExpiry` status
- [ ] AC-058: For users with no active subscription (`None` status), the section displays a CTA linking to `/pricing`

Spec ref: 0016
Status: Pending

---

### US-013: Dashboard Overview Page Loads

**As a** QA engineer validating the authenticated landing page
**I want to** verify that the dashboard page renders correctly for the test user
**So that** regressions in the dashboard API integration or card grid are caught

#### Acceptance Criteria

- [ ] AC-059: Navigating to `/dashboard` renders the page without an error and within a reasonable timeout (5 s)
- [ ] AC-060: The page contains a "Create Project" button in the dashboard header
- [ ] AC-061: The seed project card is visible on the dashboard with its name `[E2E] Seed Project`
- [ ] AC-062: The quota usage indicator is visible above the card grid (shows "X / Y runs" or "No active plan")
- [ ] AC-063: The seed project card displays a run status badge (any valid status, including `never_run`)

Spec ref: 0010
Status: Pending

---

### US-014: Dashboard Card Navigates to Project History

**As a** QA engineer validating dashboard navigation
**I want to** verify that clicking a project card on the dashboard navigates to the per-project history page
**So that** regressions in the card click handler or route wiring are caught

#### Acceptance Criteria

- [ ] AC-064: Clicking anywhere on the seed project card (outside any edit icon) navigates to `/projects/:seedProjectId/history`
- [ ] AC-065: The browser Back button from the history page returns to `/dashboard` (client-side routing — no full reload)

Spec ref: 0010
Status: Pending

---

### US-015: Project List Page — With Seed Project

**As a** QA engineer validating the projects list
**I want to** verify that the project list renders the seed project with correct data
**So that** regressions in the project list API integration are caught

#### Acceptance Criteria

- [ ] AC-066: Navigating to `/projects` renders a page containing the seed project card with name `[E2E] Seed Project`
- [ ] AC-067: The seed project card displays the product URL `https://example.com`
- [ ] AC-068: A "Create Project" button is visible in the page header
- [ ] AC-069: Cards are displayed in a grid layout (not an empty state panel)

Spec ref: 0010b
Status: Pending

---

### US-016: Project List Page — Empty State

**As a** QA engineer validating the empty state
**I want to** verify that the project list shows a meaningful empty state when the user has no projects
**So that** regressions in the empty state UI are caught

#### Acceptance Criteria

- [ ] AC-070: Using a secondary test browser context with an authenticated user who has no projects, navigating to `/projects` shows an empty state panel (not a card grid)
- [ ] AC-071: The empty state panel contains a "Create your first project" button that navigates to `/projects/new`
- [ ] AC-072: The "Create Project" button in the page header remains visible in the empty state
- [ ] AC-073: The empty state is not shown while a fetch is in progress (skeleton is shown first, then the confirmed-empty state)

#### Edge Cases

- [ ] AC-074: Alternatively: after the suite teardown deletes all `[E2E]` projects, navigating to `/projects` shows the empty state — this satisfies the empty state coverage if a dedicated empty-state browser context is unavailable

Spec ref: 0010b
Status: Pending

---

### US-017: Project List — Create Button Navigates to Create Form

**As a** QA engineer validating project list navigation
**I want to** verify that the "Create Project" button on the project list navigates to the creation form
**So that** regressions in the create-project CTA routing are caught

#### Acceptance Criteria

- [ ] AC-075: Clicking the "Create Project" button in the project list header navigates to `/projects/new`
- [ ] AC-076: The project creation form renders at `/projects/new` with Name, Product URL, and Testing Strategy fields

Spec ref: 0010b, 0006
Status: Pending

---

### US-018: Project List — Card Edit Icon Navigates to Settings

**As a** QA engineer validating project card actions
**I want to** verify that clicking the edit icon on a project card navigates to the project settings page
**So that** regressions in the edit icon routing are caught

#### Acceptance Criteria

- [ ] AC-077: Each project card includes an edit icon button in the top-right corner
- [ ] AC-078: Clicking the edit icon on the seed project card navigates to `/projects/:seedProjectId/settings`
- [ ] AC-079: Clicking the edit icon does not also trigger the card-level history navigation

Spec ref: 0010b
Status: Pending

---

### US-019: Project Creation — Happy Path

**As a** QA engineer validating project creation
**I want to** verify that a new project can be created through the portal form and appears in the list
**So that** regressions in the project creation flow are caught

#### Acceptance Criteria

- [ ] AC-080: Navigating to `/projects/new` renders a form with required fields: Name, Product URL, and Testing Strategy
- [ ] AC-081: Submitting the form with `name: '[E2E] Created Project'`, `productUrl: 'https://created.example.com'`, and a non-empty testing strategy calls `POST /v1/projects` and navigates to the new project's settings page
- [ ] AC-082: The newly created project appears in the project list at `/projects`
- [ ] AC-083: The test cleans up the created project by calling `DELETE /v1/projects/:id` via the Playwright `request` fixture at the end of the test (inline teardown)

Spec ref: 0006
Status: Pending

---

### US-020: Project Creation — Validation Errors

**As a** QA engineer validating project creation error handling
**I want to** verify that the creation form shows inline errors for missing or invalid fields
**So that** regressions in client-side validation are caught

#### Acceptance Criteria

- [ ] AC-084: Submitting the creation form with the Name field empty shows an inline validation error on the Name field and does not navigate away
- [ ] AC-085: Submitting with a `productUrl` that is not a valid URL format (e.g. `not-a-url`) shows an inline validation error on the URL field
- [ ] AC-086: Submitting with the Testing Strategy field empty shows an inline validation error on that field
- [ ] AC-087: All three validation errors can appear simultaneously when all three fields are invalid

Spec ref: 0006
Status: Pending

---

### US-021: Project Settings Page — Settings Tab Pre-populated

**As a** QA engineer validating project settings
**I want to** verify that the Settings tab is pre-populated with the seed project's values
**So that** regressions in the settings form load are caught

#### Acceptance Criteria

- [ ] AC-088: Navigating to `/projects/:seedProjectId/settings` renders the Settings tab by default
- [ ] AC-089: The Name field is pre-populated with `[E2E] Seed Project`
- [ ] AC-090: The Product URL field is pre-populated with `https://example.com`
- [ ] AC-091: The Testing Strategy field is pre-populated with the value set at seed creation time
- [ ] AC-092: The custom prompt field is visible and accepts text input

Spec ref: 0006, 0008, 0010c
Status: Pending

---

### US-022: Project Settings Page — Save Updates Project

**As a** QA engineer validating project settings persistence
**I want to** verify that changing a field on the Settings tab and saving calls the API and shows a success state
**So that** regressions in the project update flow are caught

#### Acceptance Criteria

- [ ] AC-093: Modifying the Testing Strategy field enables the Save Changes button (previously disabled in clean state)
- [ ] AC-094: Clicking Save Changes calls `PUT /v1/projects/:id` (or `PATCH`) with the updated value
- [ ] AC-095: A success indication is shown after the save (button transitions to "Saved ✓" state or a success snackbar appears)
- [ ] AC-096: After saving, refreshing the page shows the updated value still pre-populated

Spec ref: 0006, 0010c
Status: Pending

---

### US-023: Project Settings Page — Save Validation Errors

**As a** QA engineer validating settings save validation
**I want to** verify that clearing a required field on the Settings tab shows a validation error before the API is called
**So that** regressions in save-time validation are caught

#### Acceptance Criteria

- [ ] AC-097: Clearing the Name field and clicking Save Changes shows an inline validation error on the Name field and does not call the API
- [ ] AC-098: Clearing the Product URL field and clicking Save Changes shows an inline validation error on the URL field and does not call the API
- [ ] AC-099: The Save Changes button is re-enabled after a validation error so the user can correct and retry

Spec ref: 0006, 0010c
Status: Pending

---

### US-024: Project Settings Page — Report Format & Attachments Section Visible

**As a** QA engineer validating the Settings tab layout
**I want to** verify that the Report Format & Attachment Settings section is present on the Settings tab
**So that** regressions in the report settings section layout are caught

#### Acceptance Criteria

- [ ] AC-100: The Settings tab contains a "Report Format & Attachment Settings" section (or equivalent heading)
- [ ] AC-101: Two attachment toggles are visible: "Include step-by-step logs" and "Include screenshots"
- [ ] AC-102: A report template upload control or current template indicator is visible on the tab

Spec ref: 0009, 0010c
Status: Pending

---

### US-025: Project Settings Page — Integration Tab Accessible

**As a** QA engineer validating the Integration tab
**I want to** verify that the Integration tab is accessible and renders its form without a page error
**So that** regressions in the integration settings UI are caught

#### Acceptance Criteria

- [ ] AC-103: Clicking the "Integration" tab on `/projects/:seedProjectId/settings` renders the PM tool integration form without a page error
- [ ] AC-104: The Integration tab form contains a PM tool type selector (Azure DevOps / Jira)
- [ ] AC-105: Switching between the Settings and Integration tabs does not reload the page (client-side tab switch — URL does not change or changes only its hash/query, no full reload)
- [ ] AC-106: The unsaved-changes banner appears when returning to the Settings tab after making a change

Spec ref: 0007, 0010c
Status: Pending

---

### US-026: Integration Tab — Configure ADO Token

**As a** QA engineer validating ADO integration configuration
**I want to** verify that the ADO form renders correctly and accepts input
**So that** regressions in the ADO integration form are caught

#### Acceptance Criteria

- [ ] AC-107: Selecting "Azure DevOps" from the PM tool type selector reveals the ADO-specific form fields: Organization URL, Project Name, Team, "In Testing" Status Name, and Auth Method selector
- [ ] AC-108: Selecting PAT as the auth method reveals a PAT token field
- [ ] AC-109: Submitting the ADO form with all required fields populated (using test/mock values) calls the settings save without a page error
- [ ] AC-110: Submitting with required fields empty shows inline validation errors on the empty fields

Spec ref: 0007
Status: Pending

---

### US-027: Integration Tab — Configure Jira Token

**As a** QA engineer validating Jira integration configuration
**I want to** verify that the Jira form renders correctly and accepts input
**So that** regressions in the Jira integration form are caught

#### Acceptance Criteria

- [ ] AC-111: Selecting "Jira" from the PM tool type selector reveals the Jira-specific form fields: Base URL, Project Key, "In Testing" Status Name, and Auth Method selector
- [ ] AC-112: Selecting "API Token + Email" reveals both an Email field and an API Token field
- [ ] AC-113: Selecting PAT as the Jira auth method reveals only a PAT field (Email field is hidden)
- [ ] AC-114: Submitting with required fields empty shows inline validation errors

Spec ref: 0007
Status: Pending

---

### US-028: Access Mode Tab — Configure IP Allowlist

**As a** QA engineer validating environment access configuration
**I want to** verify that the IP Allowlist access mode can be selected and saved
**So that** regressions in the access mode UI are caught

#### Acceptance Criteria

- [ ] AC-115: The project settings page contains an access mode section (within the Settings or Integration tab) offering three options: "IP Allowlisting", "HTTP Basic Auth", and "Custom Header Token"
- [ ] AC-116: Selecting "IP Allowlisting" hides any credential input fields
- [ ] AC-117: The UI displays the Testurio published egress IP range alongside the IP Allowlisting option
- [ ] AC-118: Saving with "IP Allowlisting" selected calls the settings save without error

Spec ref: 0017
Status: Pending

---

### US-029: Access Mode Tab — Configure Basic Auth Credentials

**As a** QA engineer validating Basic Auth configuration
**I want to** verify that HTTP Basic Auth credentials can be entered and saved
**So that** regressions in the Basic Auth credential form are caught

#### Acceptance Criteria

- [ ] AC-119: Selecting "HTTP Basic Auth" reveals Username and Password fields
- [ ] AC-120: The Password field renders as masked input (`type="password"`)
- [ ] AC-121: Submitting with both fields populated calls the settings save without error
- [ ] AC-122: Submitting with either field empty shows an inline validation error
- [ ] AC-123: After saving, the form re-renders with Username pre-filled and Password field empty (placeholder `••••••••`)

Spec ref: 0017
Status: Pending

---

### US-030: Access Mode Tab — Configure Header Token

**As a** QA engineer validating header token configuration
**I want to** verify that a custom header token can be entered and saved
**So that** regressions in the header token form are caught

#### Acceptance Criteria

- [ ] AC-124: Selecting "Custom Header Token" reveals "Header Name" and "Header Value" fields
- [ ] AC-125: The Header Value field renders as masked input (`type="password"`)
- [ ] AC-126: Submitting with both fields populated calls the settings save without error
- [ ] AC-127: Submitting with either field empty shows an inline validation error
- [ ] AC-128: After saving, the form re-renders with Header Name pre-filled and Header Value field empty (placeholder `••••••••`)

Spec ref: 0017
Status: Pending

---

### US-031: Work Item Type Filter — Update Allowed Types

**As a** QA engineer validating work item type filtering
**I want to** verify that the allowed work item types field can be updated on the project
**So that** regressions in the work item type filter UI are caught

#### Acceptance Criteria

- [ ] AC-129: When a PM tool connection is configured for the seed project, the Integration tab shows a "Work Item Type Filter" section
- [ ] AC-130: The section displays a multi-select or chip input pre-populated with the default types for the configured PM tool
- [ ] AC-131: Adding a new type string to the list and saving calls `PATCH /v1/projects/:id` with the updated `allowedWorkItemTypes`
- [ ] AC-132: Attempting to save with an empty list shows an inline validation error: "At least one work item type must be selected"

Spec ref: 0020
Status: Pending

---

### US-032: Delete Project — Confirmation and Redirect

**As a** QA engineer validating project deletion
**I want to** verify that a project can be deleted via the Danger Zone on the settings page, with a confirmation step
**So that** regressions in the soft-delete flow or post-delete redirect are caught

#### Acceptance Criteria

- [ ] AC-133: The project settings page contains a "Danger Zone" or "Delete Project" section at the bottom
- [ ] AC-134: Clicking the delete action opens a confirmation dialog before any API call is made
- [ ] AC-135: Confirming deletion calls `DELETE /v1/projects/:id` and navigates to the project list at `/projects` (or `/dashboard`)
- [ ] AC-136: The deleted project no longer appears in the project list after deletion

#### Edge Cases

- [ ] AC-137: Cancelling the confirmation dialog leaves the project intact and the user remains on the settings page
- [ ] AC-138: The test uses a freshly created `[E2E] Delete Target` project (not the seed project) so the seed remains available for other tests

Spec ref: 0006
Status: Pending

---

### US-033: Per-Project Test History — Empty State

**As a** QA engineer validating the test history page
**I want to** verify that the history page renders a meaningful empty state when the seed project has no runs
**So that** regressions in the history empty state UI are caught

#### Acceptance Criteria

- [ ] AC-139: Navigating to `/projects/:seedProjectId/history` renders the test history page without an error
- [ ] AC-140: When the seed project has no test runs, the page shows the empty state message "No test runs yet"
- [ ] AC-141: The empty state does not display any run row or trend chart data
- [ ] AC-142: A "Project Settings" button in the page header is visible and navigates to `/projects/:seedProjectId/settings`

Spec ref: 0011
Status: Pending

---

### US-034: Per-Project Test History — With Records

**As a** QA engineer validating the test history populated state
**I want to** verify that the history page renders run records correctly when the project has existing runs
**So that** regressions in the history table, trend chart, and run-row data rendering are caught

#### Acceptance Criteria

- [ ] AC-143: Using a project that has at least one seeded `TestResult` record (created via API in `seed.setup.ts` or a dedicated history-seed step), navigating to `/projects/:historyProjectId/history` renders a history table with at least one row
- [ ] AC-144: Each table row displays: story title, verdict badge (PASSED or FAILED), a human-readable date, total duration in seconds, and a scenario pass/fail count (e.g. "2 passed / 2 total")
- [ ] AC-145: The trend chart is rendered above the table; three time-range toggle buttons (Last 7 days, Last 30 days, Last 90 days) are visible; Last 30 days is selected by default
- [ ] AC-146: Clicking the Last 7 days toggle updates the chart client-side without a page reload or API call
- [ ] AC-147: Clicking a run row opens the run detail panel without navigating away from the history page

Spec ref: 0011
Status: Pending

---

### US-035: Run Detail — Structured Scenario View

**As a** QA engineer validating the run detail panel
**I want to** verify that the run detail panel renders per-scenario results correctly
**So that** regressions in the structured report view are caught

#### Acceptance Criteria

- [ ] AC-148: Clicking a run row opens the run detail panel and fetches `GET /v1/stats/projects/:projectId/runs/:runId`
- [ ] AC-149: The panel displays the story title, overall verdict, and recommendation label
- [ ] AC-150: The panel renders one scenario card per scenario in the run; each card shows scenario title, pass/fail icon, and duration
- [ ] AC-151: Failed scenario cards display the `errorSummary` in a monospace block
- [ ] AC-152: A "Raw report" toggle button is visible in the panel header
- [ ] AC-153: Clicking "Raw report" switches the panel body to the rendered markdown view

Spec ref: 0011
Status: Pending

---

### US-036: Sidebar Navigation — Links Navigate Correctly

**As a** QA engineer validating portal navigation
**I want to** verify that the sidebar links navigate to the correct pages
**So that** regressions in routing or the layout shell are caught

#### Acceptance Criteria

- [ ] AC-154: Clicking the "Dashboard" sidebar link from any portal page navigates to `/dashboard`
- [ ] AC-155: Clicking the "Projects" sidebar link navigates to `/projects`
- [ ] AC-156: Clicking the "Settings" sidebar link navigates to `/settings`
- [ ] AC-157: Navigation is client-side — no full page reload occurs between authenticated portal pages

Spec ref: 0010a
Status: Pending

---

### US-037: Sidebar Navigation — Active Item Highlighted

**As a** QA engineer validating sidebar state
**I want to** verify that the active sidebar link is visually highlighted based on the current route
**So that** regressions in the active-link highlighting logic are caught

#### Acceptance Criteria

- [ ] AC-158: When on `/dashboard`, the "Dashboard" sidebar link has an active/selected visual style; Projects and Settings links do not
- [ ] AC-159: When on `/projects`, the "Projects" sidebar link has the active style
- [ ] AC-160: When on `/settings`, the "Settings" sidebar link has the active style
- [ ] AC-161: When on `/projects/:id/history`, the "Projects" link is treated as active (prefix match)

Spec ref: 0010a
Status: Pending

---

### US-038: Header — User Display Name Shown

**As a** QA engineer validating the portal shell header
**I want to** verify that the signed-in user's display name or avatar is present in the header on every authenticated page
**So that** regressions in the header identity rendering are caught

#### Acceptance Criteria

- [ ] AC-162: The portal header displays either the user's display name, initials, or avatar on authenticated pages
- [ ] AC-163: The header identity is present on `/dashboard`, `/projects`, and `/settings`
- [ ] AC-164: Clicking the Testurio logo in the header navigates to `/dashboard`

Spec ref: 0010a
Status: Pending

---

### US-039: Sidebar — Collapse and Expand

**As a** QA engineer validating the sidebar collapse feature
**I want to** verify that the sidebar can be collapsed to icon-only width and expanded back
**So that** regressions in the collapse/expand toggle are caught

#### Acceptance Criteria

- [ ] AC-165: A toggle button (chevron icon) is visible at the top of the sidebar
- [ ] AC-166: Clicking the toggle button collapses the sidebar to icon-only width (64 px) and hides navigation labels
- [ ] AC-167: Clicking the toggle button again expands the sidebar back to full width (240 px) and shows navigation labels
- [ ] AC-168: The collapsed state is persisted in `localStorage` (`testurio.sidebarCollapsed`) so a page refresh restores the same state

Spec ref: 0010a
Status: Pending

---

### US-040: Account Settings — Update Display Name

**As a** QA engineer validating account settings
**I want to** verify that the display name can be updated from the Account Settings page
**So that** regressions in the profile update flow are caught

#### Acceptance Criteria

- [ ] AC-169: Navigating to `/settings` renders the Account Settings page with a Personal Information section containing a Display Name field
- [ ] AC-170: The Display Name field is pre-populated with the current value
- [ ] AC-171: Updating the Display Name and clicking Save calls `PATCH /v1/account/profile` and shows a success snackbar "Settings saved"
- [ ] AC-172: After saving, the header reflects the updated display name without a full page reload

#### Edge Cases

- [ ] AC-173: Submitting with an empty Display Name shows an inline validation error "Display name is required"

Spec ref: 0014
Status: Pending

---

### US-041: Account Settings — Language and Theme Preferences

**As a** QA engineer validating preferences
**I want to** verify that language and appearance toggles are visible and functional on the Account Settings page
**So that** regressions in the preferences section are caught

#### Acceptance Criteria

- [ ] AC-174: The Account Settings page contains a Preferences section with a Language dropdown and an Appearance toggle (Light / Dark)
- [ ] AC-175: The Language dropdown lists at minimum English (en) and Ukrainian (uk)
- [ ] AC-176: Selecting Dark in the Appearance toggle immediately applies the dark theme to the portal (no Save click required for the visual change)
- [ ] AC-177: After selecting Dark and saving preferences, refreshing the page restores the Dark theme (persisted in `localStorage` and via API)

Spec ref: 0014
Status: Pending

---

### US-042: Environment Variable Configuration

**As a** QA engineer setting up the suite locally
**I want to** supply all required credentials via `.env.test` without modifying source files
**So that** no credentials are ever committed to the repository

#### Acceptance Criteria

- [ ] AC-178: The suite reads `BASE_URL`, `TEST_USER_EMAIL`, `TEST_USER_PASSWORD`, `CF_ACCESS_CLIENT_ID`, and `CF_ACCESS_CLIENT_SECRET` exclusively from `.env.test` (loaded by `playwright.live.config.ts`)
- [ ] AC-179: `.env.test` is listed in `.gitignore` and is never committed
- [ ] AC-180: The repository contains a `.env.test.example` file documenting all required variables so a new engineer knows which values to supply

Spec ref: 0049
Status: Pending

---

### US-043: Post-Suite Database Teardown

**As a** QA engineer maintaining a clean test environment
**I want to** have all projects created during the suite automatically deleted at the end of the run
**So that** repeated suite executions do not accumulate stale test data in Cosmos DB

#### Acceptance Criteria

- [ ] AC-181: A `teardown.ts` Playwright global teardown file is added under `e2e/real/` and registered in `playwright.live.config.ts` as the `globalTeardown` script
- [ ] AC-182: The teardown reads `projectId` from `e2e/.auth/seed.json` (if it exists) and calls `DELETE /v1/projects/:id` using the persisted auth state from `e2e/.auth/user.json`; it logs but does not throw if the project is already gone (404 is silently skipped)
- [ ] AC-183: The teardown also queries `GET /v1/projects` and deletes any project whose name starts with `[E2E]`, catching projects created by any test that were not cleaned up inline (e.g. due to test failure mid-run)
- [ ] AC-184: The teardown does NOT delete the Azure AD B2C test user account, the Cosmos `Users` document, or any `UserSubscriptions` document — only projects are removed
- [ ] AC-185: After the teardown runs, `e2e/.auth/seed.json` is deleted so the next run starts fresh
- [ ] AC-186: The teardown uses the Playwright `request` API authenticated via the stored `storageState` — no additional credentials are required

Spec ref: 0006, 0049
Status: Pending
