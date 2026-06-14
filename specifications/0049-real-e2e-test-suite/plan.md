# Implementation Plan — Real-Life E2E Test Suite for the Web Portal (0049)

## Tasks

- [x] T000 [Test] `e2e/real/subscription.spec.ts` — Stripe Checkout happy path: authenticated user clicks "Start free trial" on pricing page, completes Stripe Checkout with test card, lands on `/billing/success`, polls until `status: Trialing` (US-011) — `source/Testurio.Web/e2e/real/subscription.spec.ts` — **Done**

- [ ] T001 [Test] Wire `playwright.live.config.ts` — add `setup:seed` project (depends on `setup:auth`); update `chromium` project to depend on `['setup:auth', 'setup:seed']`; register `e2e/real/teardown.ts` as `globalTeardown` (US-001, US-002, US-043) — `source/Testurio.Web/playwright.live.config.ts`

- [ ] T002 [Test] Write `e2e/real/teardown.ts` — read `seed.json`, DELETE seed project, query `GET /v1/projects` and DELETE any project whose name starts with `[E2E]`, skip 404s silently, delete `seed.json` after cleanup; never touch B2C accounts, Users, or UserSubscriptions (US-043) — `source/Testurio.Web/e2e/real/teardown.ts`

- [ ] T003 [Test] Write `e2e/real/auth.spec.ts` — sign-in happy path: `/sign-in` renders form, valid credentials → `/dashboard`, display name visible, no sidebar on sign-in page (US-003); sign-in wrong password: inline error shown, no redirect (US-004); sign-out: Sign Out button clears session → `/sign-in`, post-signout `/dashboard` redirects to `/sign-in` (US-005) — `source/Testurio.Web/e2e/real/auth.spec.ts`

- [ ] T004 [Test] Write `e2e/real/route-guard.spec.ts` — unauthenticated access to `/dashboard`, `/projects`, `/settings`, `/projects/:id/settings` each redirect to `/sign-in` with a `returnUrl` param; uses a fresh browser context with no stored auth (US-006) — `source/Testurio.Web/e2e/real/route-guard.spec.ts`

- [ ] T005 [Test] Write `e2e/real/registration.spec.ts` — navigate to `/sign-up`, assert form fields, submit with generated unique email → `/dashboard`, assert `GET /v1/account/me` returns 200 within 5 s, assert display name/avatar visible, assert page not in shell layout (US-007); duplicate email: submit with `TEST_USER_EMAIL` → inline duplicate-email error with "Sign in instead?" link (US-008) — `source/Testurio.Web/e2e/real/registration.spec.ts`

- [ ] T006 [Test] Write `e2e/real/forgot-password.spec.ts` — click "Forgot password?" link on `/sign-in` → `/forgot-password`; assert form renders with Email field and submit button; submit valid email → confirmation message shown; assert "Back to sign in" link navigates to `/sign-in`; assert confirmation shown for unregistered email too; assert empty-email submission shows validation error (US-009) — `source/Testurio.Web/e2e/real/forgot-password.spec.ts`

- [ ] T007 [Test] Write `e2e/real/pricing.spec.ts` — `/pricing` accessible without auth; four plan cards visible in order; Monthly/Annual toggle visible and defaults to Monthly; switching to Annual updates prices without reload; Test Pro card marked "Most popular" (US-010) — `source/Testurio.Web/e2e/real/pricing.spec.ts`

- [ ] T008 [Test] Write `e2e/real/dashboard.spec.ts` — navigate to `/dashboard`; page loads without error; "Create Project" button visible; seed project card visible with name and run status badge; quota usage indicator visible; click seed project card → `/projects/:seedProjectId/history`; Back button returns to `/dashboard` (US-013, US-014) — `source/Testurio.Web/e2e/real/dashboard.spec.ts`

- [ ] T009 [Test] Write `e2e/real/projects-list.spec.ts` — navigate to `/projects`; seed project card visible with name `[E2E] Seed Project` and URL; "Create Project" button visible in header; "Create Project" button navigates to `/projects/new`; edit icon on seed card navigates to `/projects/:seedProjectId/settings`; edit icon click does not also navigate to history (US-015, US-017, US-018) — `source/Testurio.Web/e2e/real/projects-list.spec.ts`

- [ ] T010 [Test] Extend `e2e/real/projects-list.spec.ts` — empty state: using a browser context with no projects (or asserting empty state after teardown), navigate to `/projects`; assert empty state panel with "Create your first project" button; assert "Create Project" header button still visible; assert no card grid rendered (US-016) — `source/Testurio.Web/e2e/real/projects-list.spec.ts`

- [ ] T011 [Test] Write `e2e/real/project-create.spec.ts` — navigate to `/projects/new`; assert form with Name, Product URL, Testing Strategy fields; submit with `[E2E] Created Project` / `https://created.example.com` / test strategy → navigates to new project settings page; new project appears in `/projects`; inline API DELETE cleanup at test end (US-019); submit with empty Name → validation error; submit with invalid URL → validation error; submit with empty Testing Strategy → validation error; all three errors can appear together (US-020) — `source/Testurio.Web/e2e/real/project-create.spec.ts`

- [ ] T012 [Test] Write `e2e/real/project-settings.spec.ts` — navigate to `/projects/:seedProjectId/settings`; Settings tab active by default; Name, Product URL, Testing Strategy pre-populated; custom prompt field visible; update Testing Strategy and click Save → API called, success state shown; verify updated value persists on reload; clear Name → validation error before API call; clear URL → validation error before API call (US-021, US-022, US-023) — `source/Testurio.Web/e2e/real/project-settings.spec.ts`

- [ ] T013 [Test] Extend `e2e/real/project-settings.spec.ts` — Report Format & Attachments section visible; "Include step-by-step logs" and "Include screenshots" toggles present; report template upload control or filename indicator visible (US-024) — `source/Testurio.Web/e2e/real/project-settings.spec.ts`

- [ ] T014 [Test] Write `e2e/real/project-integration.spec.ts` — click Integration tab; form renders without page error; PM tool type selector present; tab switch between Settings and Integration is client-side (no full reload); ADO selected → ADO fields visible (Org URL, Project Name, Team, Status, Auth Method); PAT selected → PAT field visible; submit with empty required ADO fields → validation errors; Jira selected → Jira fields visible (Base URL, Project Key, Status, Auth Method); API Token + Email selected → Email + Token fields; PAT selected → only PAT field; empty Jira submit → validation errors (US-025, US-026, US-027) — `source/Testurio.Web/e2e/real/project-integration.spec.ts`

- [ ] T015 [Test] Write `e2e/real/project-access.spec.ts` — access mode section visible with three options; select IP Allowlisting → credential fields hidden, egress IP range displayed, save succeeds; select HTTP Basic Auth → Username and Password fields shown; Password renders masked; submit with both fields → save succeeds; submit with empty fields → validation errors; after save Username pre-filled, Password empty with placeholder; select Custom Header Token → Header Name and Header Value fields shown; Header Value renders masked; submit with both → save succeeds; empty fields → validation errors; after save Header Name pre-filled, value empty with placeholder (US-028, US-029, US-030) — `source/Testurio.Web/e2e/real/project-access.spec.ts`

- [ ] T016 [Test] Write `e2e/real/work-item-filter.spec.ts` — with ADO integration configured on seed project: Integration tab shows Work Item Type Filter section; default types pre-populated for ADO (`["User Story", "Bug"]`); add a new type and save → `PATCH /v1/projects/:id` called with updated list; attempt to save with empty list → validation error (US-031) — `source/Testurio.Web/e2e/real/work-item-filter.spec.ts`

- [ ] T017 [Test] Write `e2e/real/project-delete.spec.ts` — create `[E2E] Delete Target` project via API in test setup; navigate to its settings page; Danger Zone / Delete Project section visible; click delete → confirmation dialog shown; cancel → project intact, still on settings page; confirm deletion → `DELETE /v1/projects/:id` called → redirect to `/projects` (or `/dashboard`); deleted project no longer appears in list (US-032) — `source/Testurio.Web/e2e/real/project-delete.spec.ts`

- [ ] T018 [Test] Write `e2e/real/project-history.spec.ts` — navigate to `/projects/:seedProjectId/history`; page renders without error; empty state message "No test runs yet" shown; no run rows or trend chart rendered; "Project Settings" button visible → navigates to settings (US-033) — `source/Testurio.Web/e2e/real/project-history.spec.ts`

- [ ] T019 [Test] Extend `e2e/real/project-history.spec.ts` — seed a `TestResult` record via API in `seed.setup.ts` (or a dedicated history seed step) for a second `[E2E] History Project`; navigate to its history page; table renders with ≥1 row; each row shows story title, verdict badge, date, duration, scenario count; trend chart visible above table; Last 30 days selected by default; switching to Last 7 days updates chart without reload; clicking a run row opens the detail panel; panel shows story title, verdict, recommendation, scenario cards; "Raw report" toggle visible; clicking it switches panel to markdown view (US-034, US-035) — `source/Testurio.Web/e2e/real/project-history.spec.ts`

- [ ] T020 [Test] Write `e2e/real/navigation.spec.ts` — from `/dashboard`: click Projects link → `/projects`; click Settings link → `/settings`; from `/settings`: click Dashboard link → `/dashboard`; all navigations client-side; assert active link style on Dashboard when at `/dashboard`; on Projects when at `/projects`; on Settings when at `/settings`; on Projects when at `/projects/:id/history` (prefix match) (US-036, US-037) — `source/Testurio.Web/e2e/real/navigation.spec.ts`

- [ ] T021 [Test] Write `e2e/real/header.spec.ts` — assert user display name / avatar present in header on `/dashboard`, `/projects`, and `/settings`; assert Testurio logo in header; clicking logo navigates to `/dashboard`; assert sidebar collapse toggle visible; click toggle → sidebar collapses to icon-only; navigation labels hidden; click toggle again → sidebar expands; assert `localStorage` key `testurio.sidebarCollapsed` persists state across reload (US-038, US-039) — `source/Testurio.Web/e2e/real/header.spec.ts`

- [ ] T022 [Test] Write `e2e/real/account-settings.spec.ts` — navigate to `/settings`; Personal Information section with Display Name field pre-populated; update display name → save → success snackbar; header updates display name; empty name → validation error; Preferences section with Language dropdown (at least en, uk options) and Appearance toggle (Light/Dark); select Dark → dark theme applied immediately; save → refresh → dark theme persists (US-040, US-041) — `source/Testurio.Web/e2e/real/account-settings.spec.ts`

- [ ] T023 [Test] Write `e2e/real/billing-status.spec.ts` — navigate to `/settings` (billing tab or subscription section); for user with `Trialing` or `Active` subscription: plan name, billing interval, period end date visible; "Manage billing" button visible; for user with `None` subscription: CTA linking to `/pricing` shown (US-012) — `source/Testurio.Web/e2e/real/billing-status.spec.ts`

- [ ] T024 [Test] Verify `.env.test.example` documents all required variables (`BASE_URL`, `TEST_USER_EMAIL`, `TEST_USER_PASSWORD`, `CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET`) and `.env.test` is listed in `.gitignore` (US-042) — `source/Testurio.Web/.env.test.example`, `source/Testurio.Web/.gitignore`

---

## Rationale

**T000 (already done) — subscription.spec.ts.** The Stripe Checkout flow is the most complex and highest-risk E2E path; it was implemented first to validate the real-environment test harness. All subsequent tasks inherit its setup patterns.

**T001 before everything else — config is the prerequisite.** Registering `setup:seed` and `globalTeardown` in `playwright.live.config.ts` must happen before any test that depends on seed data or post-suite cleanup can run. Both concerns touch a single config file and belong in one task.

**T002 (teardown) alongside T001.** The teardown file is referenced by `globalTeardown` in T001's config change. It is listed separately so it has its own checkbox and file path, but it is implemented in the same work unit as T001.

**T003 (auth) before project tests.** Auth is the foundational flow. If the session mechanism or sign-in form regresses, all subsequent tests fail with misleading errors. Isolating auth first makes triage faster.

**T004 (route guard) after T003.** Route guard uses a fresh browser context without stored auth — it must be confirmed independently of the happy-path session. Comes after T003 so the guard pattern is understood in context.

**T005 (registration) and T006 (forgot-password) after T003.** Both are separate B2C policy flows that share the sign-in page entry point confirmed in T003. Registration uses a generated email to avoid collision with the permanent test account.

**T007 (pricing) is public and session-independent.** It can be verified at any time but is placed after auth tests so the suite's test organisation follows the user journey: public → auth → portal.

**T008 (dashboard) before T009 (project list).** The dashboard is the authenticated landing page and the primary entry to projects. Card-click navigation to history is validated here so T009 can focus on the list-specific interactions.

**T009/T010 (project list) before T011 (project create).** List is a read path requiring only the seed; create is a write path that also verifies the result appears in the list. Read before write.

**T011 (project create) before T012–T015 (settings tabs).** Project creation ends with a redirect to the settings page — confirming that redirect is the navigation entry point for the settings tests.

**T012/T013 (Settings tab) before T014 (Integration tab).** The Settings tab is the default view; Integration tab relies on the tab-switch mechanism proved by T012.

**T015 (access mode) can run parallel to T014 but placed after.** Access mode is a subsection of the settings/integration area. Separated into its own file because it involves credential masking and mode-switching behaviour distinct from PM tool configuration.

**T016 (work item filter) after T014.** The Work Item Type Filter section is only visible when a PM tool connection is configured — the Integration tab test in T014 must confirm the filter section appears correctly first.

**T017 (project delete) after all settings tests.** Delete is destructive and uses a dedicated project (not the seed) to avoid affecting other tests. Placed last in the project-settings group.

**T018/T019 (test history) after T017.** History is a read-only view of a separate data domain. T018 covers empty state (seed project, no runs), T019 covers populated state (requires a seeded `TestResult` record).

**T020 (navigation) after project and history tests.** Sidebar navigation is exercised implicitly by every other test. Isolating it after the feature group ensures it surfaces only navigation-specific regressions.

**T021 (header/sidebar collapse) near last.** The header is visible on every authenticated page; collapse/expand is a UI-only interaction with no data dependency.

**T022 (account settings) before billing status.** Account settings is the settings page's personal information section; billing status is a sub-section or adjacent tab. Personal info first, then billing.

**T023 (billing status) near last.** Billing status depends on a `Trialing` subscription (confirmed by T000). It is a read-only display assertion with no write dependency beyond what T000 already established.

**T024 (env vars / .gitignore) last.** Documentation and hygiene task with no code dependency; verified at any point but lowest implementation priority.

---

## Layer Tags

| Tag      | Scope                                                        |
| -------- | ------------------------------------------------------------ |
| `[Test]` | Playwright real E2E test files, setup/teardown, and config   |

---

## Cross-Feature Dependencies

| Task(s) | Depends on Feature |
|---------|-------------------|
| T003–T006 | 0013 (Registration & Sign-In) — B2C sign-in, sign-up, forgot-password pages must exist |
| T008 | 0010 (Dashboard) — card grid, quota bar, SSE stream |
| T009, T010 | 0010b (Project List Page) — card grid, empty state, edit icon |
| T011 | 0006 (Project Creation) — creation form and `POST /v1/projects` |
| T012, T013 | 0006, 0008, 0009, 0010c (Settings tab layout and unified Save button) |
| T014 | 0007, 0010c (Integration tab) |
| T015 | 0017 (Access mode configuration) |
| T016 | 0020 (Work item type filter) |
| T017 | 0006 (Soft delete, confirmation dialog, post-delete redirect) |
| T018, T019 | 0011 (Test history page — empty and populated states) |
| T020, T021 | 0010a (Sidebar shell, collapse/expand, active-link highlighting) |
| T022 | 0014 (Account Settings — profile and preferences) |
| T023 | 0015, 0016 (Plan purchase, subscription management) |
| T007 | 0012, 0015 (Marketing/pricing pages, plan purchase CTA) |
