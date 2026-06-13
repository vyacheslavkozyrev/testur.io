# Implementation Plan — Real-Life E2E Test Suite for the Web Portal (0049)

## Tasks

- [ ] T001 [Test] Wire `seed.setup.ts` into `playwright.live.config.ts` as a `setup:seed` project with dependency on `setup:auth`; update the `chromium` project to depend on `['setup:auth', 'setup:seed']`; register `teardown.ts` as `globalTeardown` (US-002, US-013) — `source/Testurio.Web/playwright.live.config.ts`
- [ ] T002 [Test] Write `e2e/real/auth.spec.ts` — sign-in redirect to B2C hosted UI, post-login dashboard landing, authenticated display name visible, sign-out clears session, post-signout `/dashboard` redirect guard (US-003) — `source/Testurio.Web/e2e/real/auth.spec.ts`
- [ ] T003 [Test] Write `e2e/real/registration.spec.ts` — navigate to sign-up, complete B2C registration form with generated email, assert redirect to `/dashboard`, assert `GET /v1/account/me` returns `200` within 5 s, assert display name/avatar visible (US-011) — `source/Testurio.Web/e2e/real/registration.spec.ts`
- [ ] T004 [Test] Write `e2e/real/forgot-password.spec.ts` — click "Forgot password" on B2C sign-in page, assert password-reset policy page loads, enter email and assert verification-code step is reached, complete reset and assert redirect destination, tag test `@slow` (US-012) — `source/Testurio.Web/e2e/real/forgot-password.spec.ts`
- [ ] T005 [Test] Write `e2e/real/projects-list.spec.ts` — seed project card visible with correct name and URL, "Create Project" button present, card click navigates to settings (US-004) — `source/Testurio.Web/e2e/real/projects-list.spec.ts`
- [ ] T006 [Test] Write `e2e/real/project-create.spec.ts` — `/projects/new` form renders, submit creates project and navigates to new project settings page, new project appears in list, inline cleanup via API DELETE (US-005) — `source/Testurio.Web/e2e/real/project-create.spec.ts`
- [ ] T007 [Test] Write `e2e/real/project-settings.spec.ts` — Settings tab pre-populated with seed project values, save calls `PUT /v1/projects/:id` and shows success toast, custom prompt field visible, report format section visible (US-006) — `source/Testurio.Web/e2e/real/project-settings.spec.ts`
- [ ] T008 [Test] Write `e2e/real/project-integration.spec.ts` — Integration tab accessible without page error, PM tool type selector present, client-side tab switch between Settings and Integration does not reload the page (US-007) — `source/Testurio.Web/e2e/real/project-integration.spec.ts`
- [ ] T009 [Test] Write `e2e/real/project-history.spec.ts` — history page renders without error, empty state message shown when seed project has no test runs, no run cards or trend charts visible in empty state (US-008) — `source/Testurio.Web/e2e/real/project-history.spec.ts`
- [ ] T010 [Test] Write `e2e/real/navigation.spec.ts` — sidebar Dashboard/Projects/Settings links navigate to correct routes, active link style matches current route (US-009) — `source/Testurio.Web/e2e/real/navigation.spec.ts`
- [ ] T011 [Test] Write `e2e/real/teardown.ts` — read `seed.json`, DELETE seed project, query `GET /v1/projects` and DELETE any project whose name starts with `[E2E]`, skip 404s, delete `seed.json` after cleanup; does NOT touch B2C accounts or Cosmos user documents (US-013) — `source/Testurio.Web/e2e/real/teardown.ts`
- [ ] T012 [Test] Verify `.env.test.example` documents all required variables (`BASE_URL`, `TEST_USER_EMAIL`, `TEST_USER_PASSWORD`, `CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET`) and `.env.test` is in `.gitignore` (US-010) — `source/Testurio.Web/.env.test.example`, `source/Testurio.Web/.gitignore`

## Rationale

**T001 first — config is the prerequisite for everything else.** `seed.setup.ts` already exists but is not wired into `playwright.live.config.ts`. Registering `setup:seed`, threading it into the `chromium` project's dependency chain, and registering `teardown.ts` as `globalTeardown` must happen before any test that uses seed data or teardown can run. Both concerns are one-line config changes and belong in a single task.

**T002 (sign-in/sign-out) before project tests.** Auth is the foundational flow. If the session mechanism regresses, all subsequent tests will fail with misleading errors. Isolating auth first makes triage faster.

**T003 (registration) after T002.** Registration is a separate B2C policy from sign-in. It depends on the same B2C hosted-UI pattern confirmed in T002 and uses a generated email so it never interferes with the permanent test account.

**T004 (forgot password) after T003.** Password-reset is a third B2C policy. It is tagged `@slow` because it triggers a real email send. Ordering it after the other auth flows keeps auth-related specs grouped together.

**T005 (project list) before T006 (project create).** The list page is a read path that only requires the seed; create is a write path that also verifies the result appears in the list. Read before write mirrors the data dependency.

**T006 (project create) before T007/T008 (project settings/integration).** Project creation ends with a redirect to the settings page — confirming that redirect works validates the navigation entry point for T007 and T008.

**T007 (settings tab) and T008 (integration tab) as separate tasks** — they map to distinct user stories (US-006, US-007) with different acceptance criteria and different form sections. Splitting them keeps each test file focused and failure triage straightforward.

**T009 (project history) after T007/T008.** History is a read-only view of a separate data domain (test runs). No ordering dependency on settings tests, but keeping it after completes the natural project-page group.

**T010 (navigation) near last.** Sidebar navigation is exercised implicitly by every other test (each test navigates somewhere). Isolating it late means it surfaces only navigation-specific regressions, not noise from earlier partial page loads.

**T011 (teardown) implemented alongside T001 config wiring.** The teardown file is referenced by `globalTeardown` in T001's config change. Both are small and tightly coupled; T011 is listed separately so it has its own checkbox and file path.

**T012 (env vars / .gitignore) last.** This is a documentation and hygiene task with no code dependencies. It can be verified at any point but is lowest priority during implementation.

**All tasks are [Test] layer only.** This feature adds no new backend endpoints, frontend components, or domain models. The entire scope is Playwright test files and config changes.

**Cross-feature dependencies:**
- Depends on feature `0013` (Registration & Sign-In) — B2C sign-in, sign-up, and password-reset policies must be configured for T002–T004.
- Depends on features `0006` and `0010b` — project creation API and project list page must be implemented for T005–T006.
- Depends on feature `0010c` — the Settings/Integration tab layout must exist for T007–T008.
- Depends on feature `0011` — the history page must be implemented for T009.
- Depends on feature `0010a` — the sidebar shell must be present for T010.

**No overlap** with existing mock-based specs (`e2e/dashboard.spec.ts`, `e2e/projects-list.spec.ts`, etc.) — real tests live exclusively under `e2e/real/` and are matched by the `chromium` project's `testMatch: /real\/.+\.spec\.ts/` glob, keeping both suites fully independent.

## Layer Tags

| Tag      | Scope                                                        |
| -------- | ------------------------------------------------------------ |
| `[Test]` | Playwright real E2E test files, setup/teardown, and config   |
