# Progress — Marketing & Pricing Pages (0012)

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

_Populated by `/implement 0012`_

---

## Review — 2026-05-17

### Warnings fixed
- `source/Testurio.Api/Endpoints/PlanEndpoints.cs:17,24` — `GetPlansAsync` is a synchronous method (no `Task` return type); renamed to `GetPlans` to remove the misleading `Async` suffix per be.md async naming conventions.

### Blockers fixed
- `source/Testurio.Web/src/views/PricingPage/PricingPage.tsx:85` — Skeleton grid used `md: 3` (4 columns from 900 px) while plan cards used `lg: 3` (4 columns from 1200 px), causing a layout inconsistency on 900–1200 px viewports (AC-037). Changed skeleton breakpoint to `lg: 3` to match the loaded card grid.

### Remaining issues (if any)
- `source/Testurio.Web/src/views/LandingPage/LandingPage.tsx` and `source/Testurio.Web/src/views/PricingPage/PricingPage.tsx` — Pages implemented under `src/views/` instead of the `src/pages/` path specified in plan.md and the ui.md file naming convention. Files are functional but deviate from the project's declared folder convention. Requires manual decision on whether to relocate — requires manual resolution.
- `source/Testurio.Web/e2e/marketing.spec.ts` — T026 (E2E tests) was not implemented and is marked incomplete in plan.md. Requires manual resolution before the Test phase.

### Status: Complete

---

## Test Results

### Frontend — 2026-05-17

**Component tests** (Jest):
- `PublicHeader.test.tsx` — 5 passed
- `PlanCard.test.tsx` — 10 passed
- `PricingPage.test.tsx` (in `views/`) — 8 passed

**Total: 23 frontend component tests passed, 0 failed**

**E2E tests** (Playwright — Chromium):
- `e2e/marketing.spec.ts` — 25 passed, 0 failed

Tests cover all acceptance criteria: AC-001 through AC-043.

### Bugs fixed during test phase

- `src/components/HeroSection/HeroSection.tsx` — CTA buttons had fixed `minWidth: 180` causing horizontal overflow at 375 px viewport (AC-003). Changed to responsive `minWidth: { xs: 140, sm: 180 }`.
- `src/components/PublicHeader/PublicHeader.tsx` — Header toolbar overflowed at 375 px because the "Sign In" link + "Get Started" button + hamburger icon exceeded usable width. Changed: (1) reduced CTA `minWidth` to `{ xs: 90, sm: 120 }`, (2) hid the "Sign In" link on mobile viewports (accessible via hamburger drawer).

### T026 E2E spec — created during test phase

`source/Testurio.Web/e2e/marketing.spec.ts` — 25 tests covering all acceptance criteria across landing page, public header, and pricing page.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
