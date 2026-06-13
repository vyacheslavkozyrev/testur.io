# Implementation Plan — Real-Life E2E Test Suite for the Web Portal (0049)

## Tasks

- [ ] T001 [Test] Wire `seed.setup.ts` into `playwright.live.config.ts` as a `setup:seed` project with dependency on `setup:auth`; update the `chromium` project to depend on both `['setup:auth', 'setup:seed']` — `source/Testurio.Web/playwright.live.config.ts`
- [ ] T002 [Test] Write `e2e/real/auth.spec.ts` — sign-in redirect, post-login dashboard landing, sign-out clears session, post-signout redirect guard — `source/Testurio.Web/e2e/real/auth.spec.ts`
- [ ] T003 [Test] Write `e2e/real/projects-list.spec.ts` — seed project card visible with correct name and URL, Create Project button present, card click navigates to settings — `source/Testurio.Web/e2e/real/projects-list.spec.ts`
- [ ] T004 [Test] Write `e2e/real/project-create.spec.ts` — `/projects/new` form renders, submit creates project, new project appears in list, cleanup via API DELETE — `source/Testurio.Web/e2e/real/project-create.spec.ts`
- [ ] T005 [Test] Write `e2e/real/project-settings.spec.ts` — Settings tab pre-populated, save calls PUT and shows success toast, Integration tab accessible, tab switching is client-side — `source/Testurio.Web/e2e/real/project-settings.spec.ts`
- [ ] T006 [Test] Write `e2e/real/project-history.spec.ts` — history page renders without error, empty state message present when seed project has no runs — `source/Testurio.Web/e2e/real/project-history.spec.ts`
- [ ] T007 [Test] Write `e2e/real/navigation.spec.ts` — sidebar Dashboard/Projects/Settings links navigate to correct routes, active link style matches current route — `source/Testurio.Web/e2e/real/navigation.spec.ts`

## Rationale

**T001 must come first** — `seed.setup.ts` already exists but is not wired into `playwright.live.config.ts`. Until the config registers `setup:seed` and threads it into the `chromium` project's dependency list, no real test can rely on seed data. Fixing the config is a prerequisite for every subsequent test file.

**T002 (auth flow) before project tests** — sign-in and sign-out are foundational flows. If the auth session mechanism regresses, all other tests will fail with misleading errors. Testing auth in isolation first makes triage faster.

**T003 (project list) before T004 (project create)** — the list page is a read path that only depends on the seed; create is a write path that also verifies the result appears in the list. Read before write mirrors the dependency.

**T004 (project create) before T005 (project settings)** — settings tests operate on the seed project, but verifying that the creation flow works end-to-end (including the redirect to settings) must be confirmed before the settings page itself is exercised independently.

**T005 (project settings) before T006 (project history)** — settings is the natural next step after creation. History is a read-only view of a separate data domain (test runs) and has no dependency on the settings tests, so it can come after without risk.

**T007 (navigation) last** — sidebar navigation is a cross-cutting concern exercised implicitly by every other test. Isolating it at the end means it will surface only navigation-specific regressions, not noise from partial page loads during earlier tests.

**All tasks are [Test] layer only.** This feature adds no new backend endpoints, frontend components, or domain models. The entire scope is Playwright test files and a one-line config change. No migration, domain, infrastructure, or UI layer tasks are required.

**Cross-feature dependencies:**
- Depends on feature `0013` (Registration & Sign-In) — the sign-in page and Azure AD B2C integration must be working for `auth.setup.ts` to succeed.
- Depends on features `0006` and `0010b` — project creation API and project list page must be implemented.
- Depends on feature `0010c` — the Settings/Integration tab layout must exist for T005 to be meaningful.
- Depends on feature `0011` — the history page must be implemented for T006 to navigate to it.
- Depends on feature `0010a` — the sidebar shell must be present for T007 to find navigation links.

**No overlap** with existing mock-based specs (`e2e/dashboard.spec.ts`, `e2e/projects-list.spec.ts`, etc.) — real tests live exclusively under `e2e/real/` and are matched by the `chromium` project's `testMatch: /real\/.+\.spec\.ts/` glob, keeping both suites fully independent.

## Layer Tags

| Tag      | Scope                                                        |
| -------- | ------------------------------------------------------------ |
| `[Test]` | Playwright real E2E test files and config wiring             |
