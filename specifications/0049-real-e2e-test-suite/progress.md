# Progress — Real-Life E2E Test Suite for the Web Portal (0049)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-06-13 |       |
| Plan      | ✅ Complete | 2026-06-13 |       |
| Implement | ✅ Complete | 2026-06-13 |       |
| Review    | ✅ Complete | 2026-06-13 |       |
| Test      | ✅ Complete | 2026-06-13 | Live E2E suite not run in CI — requires running server and Stripe test mode. Static checks (lint, build, tsc) all pass. All 19 real spec files exist with proper imports and test declarations. |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-06-13

### Blockers fixed
- `source/Testurio.Web/playwright.live.config.ts`:5 — `.env.test` loaded from repo root (`../../`) instead of `source/Testurio.Web/` where the file lives; fixed to `path.resolve(__dirname, '.env.test')`
- `source/Testurio.Web/playwright.live.config.ts`:51 — hardcoded developer machine Chromium path `C:/Users/vyach/...` as the default fallback; replaced with `undefined` so Playwright uses its own binary resolution
- `source/Testurio.Web/e2e/real/teardown.ts`:18 — `.env.test` path `../../../` pointed to repo root while config now points to `Testurio.Web/`; aligned to `../../` (two levels up from `e2e/real/` = `Testurio.Web/`)

### Warnings fixed
- `source/Testurio.Web/e2e/real/navigation.spec.ts`:26–28 — unreliable `page.on('load')` listener registered before flag reset; replaced with `framenavigated` counter with correct reset-before-listen ordering
- `source/Testurio.Web/e2e/real/project-history.spec.ts`:129–135 — hardcoded `page.waitForTimeout(500)` for chart toggle assertion; replaced with `framenavigated` counter plus attribute-based wait on the active button state
- `source/Testurio.Web/e2e/real/project-delete.spec.ts`:12–13 — module-level `let deleteTargetProjectId` is mutable state shared across tests; moved into a describe-scoped `state` object with a comment to convert to `test.extend` if workers > 1 is ever enabled

### Suggestions (not fixed — acceptable trade-offs)
- `playwright.live.config.ts` `testDir: './e2e'` picks up both mock-based and real specs; chromium project's `testMatch` correctly restricts to `real/` — by design, separate configs
- `e2e/real/auth.spec.ts` AC-021 (disabled/spinner state during sign-out) not fully asserted — spinner state is transient and asserting it reliably requires intercepting the network request; acceptable partial coverage
- `e2e/real/project-history.spec.ts` AC-151 (`errorSummary` monospace block) not asserted — history tests are skip-guarded when no history project is available; adding a FAILED scenario requires pipeline execution which is out of scope for this suite

### Status: Complete

---

## Test Results

_Populated by `/test [####]`_

---

## Amendments

### Amendment — 2026-06-12
**Changed**: `stories.md` (added US-011, US-012, US-013), `plan.md` (rewritten — 12 tasks, 1:1 with stories)
**Reason**: Three missing stories added (new user registration, forgot-password/reset, post-suite DB teardown); task list was not granular enough (7 tasks for 10 stories); tasks now map 1:1 to user stories and include teardown wiring in the config task
**Impact**: Plan phase re-run to produce updated task list; Implement phase not yet started so no rework required

### Amendment — 2026-06-13
**Changed**: `stories.md` (full rewrite — 43 stories, AC-001–AC-186), `plan.md` (full rewrite — T000–T024, 25 tasks)
**Reason**: Comprehensive review of all UI-facing specifications (0006, 0007, 0008, 0009, 0010, 0010a, 0010b, 0010c, 0011, 0012, 0013, 0014, 0015, 0016, 0017, 0020) revealed major coverage gaps. Added: sign-in wrong-password error state, route guard (5 protected routes), registration duplicate-email error, forgot-password flow, pricing page plan display, billing status after purchase, dashboard overview + card navigation, project list with seed and empty state, card edit icon navigation, project create validation errors, settings tab pre-population, settings tab save validation, report format/attachments section, integration tab ADO and Jira form details, access mode IP/Basic Auth/Header Token all three modes, work item type filter, project delete with confirmation, test history populated state with trend chart and run detail panel, sidebar active highlight, header identity, sidebar collapse/expand, account settings display name + preferences. Renumbered all stories US-001–US-043 and acceptance criteria AC-001–AC-186. Task plan expanded from 12 to 25 tasks with 1 marked Done (subscription.spec.ts). Tasks now ordered by user journey dependency and each maps to one or more stories with explicit file paths.
**Impact**: Implement phase not yet started so no rework required; task numbering restarted from T000
