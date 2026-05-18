# Progress — Account Settings (0014)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-18 |       |
| Plan      | ✅ Complete | 2026-05-18 |       |
| Implement | ✅ Complete | 2026-05-18 |       |
| Review    | ✅ Complete | 2026-05-18 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review

### Review — 2026-05-18

**Pre-flight**: 1,877 lines changed across 32 files. All plan.md file paths verified except `src/mocks/handlers/index.ts` (T017) — the implementation correctly registered handlers via `browser.ts` and `server.ts` directly; the plan.md path reference was stale. No Blocker from this as the handlers are properly registered.

**Findings and fixes:**

| # | Severity | File | Finding | Fix Applied |
|---|----------|------|---------|-------------|
| 1 | Warning | `accountService.ts` | `getProfile()` method was dead code — never called by any component or hook. Profile data is already fetched via `authService.getSession` (`/api/auth/me`). | Removed the unused `getProfile` method and its `AuthUser` import. |
| 2 | Warning | `account.types.ts` | `AccountProfileDto` type was missing — the profile response shape was inlined as `{ userId: string; displayName: string | null }` in the service, violating DRY and losing type traceability. | Added `AccountProfileDto` interface to `account.types.ts`; updated service and hook to use it. |
| 3 | Warning | `mocks/handlers/account.ts` | MSW `GET /v1/account/profile` handler was dead (no component called `getProfile`). Mock also lacked explicit type on `mockProfile`. | Removed unused GET mock; typed `mockProfile` as `AccountProfileDto`. |
| 4 | Warning | `AccountSettingsPage.tsx` | Called `useQuery` directly inside a component, violating the ui.md rule "Never call `useQuery`/`useMutation` directly inside a component". | Added `useAuthUserQuery` hook to `useAccount.ts`; refactored page to consume the hook. |
| 5 | Warning | `useAccount.ts` / `useUpdateProfile` | Used `queryClient.setQueryData` to update the header name after profile save, but AC-003 specifies "invalidate the React Query cache for `AUTH_KEYS.me`". Using `setQueryData` skips the server round-trip and can serve stale data. | Changed to `queryClient.invalidateQueries({ queryKey: AUTH_KEYS.me })` to match spec. |

**Remaining issues after fix**: None. All findings resolved in a single iteration.

---

## Test Results

### Test Phase — 2026-05-18

**Execution Summary:**

Backend tests could not run due to unrelated compilation errors in HttpExecutorTests.cs (missing logger parameters). However, the Account-specific test classes exist and are correctly structured:
- `tests/Testurio.UnitTests/Services/AccountServiceTests.cs` — 8 tests (account profile and preferences operations)
- `tests/Testurio.IntegrationTests/Controllers/AccountControllerTests.cs` — 8 tests (all four endpoints + auth validation)

Frontend test execution:
```
Test Suites: 1 failed, 2 passed, 3 total
Tests: 1 failed, 13 passed, 14 total
```

**Test Results by Component:**

| Component | Tests | Status | Coverage |
|-----------|-------|--------|----------|
| PersonalInfoSection | 6 | ✓ PASS | AC-002, AC-004, AC-005, AC-006, AC-007, AC-008 |
| PreferencesSection | 5 | ✓ PASS | AC-010, AC-011, AC-012, AC-013, AC-014, AC-018, AC-019 |
| AccountSettingsPage | 4 | ⚠️ 1 FAIL, 3 PASS | AC-025, AC-026, AC-027 ✓; AC-028 ✗ |

**Failing Test:**

Test: `AccountSettingsPage › shows preferences error banner when preferences fetch fails with non-404`
- Expected: Error banner "Failed to load preferences. Please refresh the page." appears
- Actual: Component remains in loading state (skeletons visible)
- Root cause: The component's `isLoading` state does not transition to false after the preferences query rejects; the queries appear not to settle in the test environment

**Missing Tests:**

Task T031 (E2E tests) was planned but not implemented:
- [ ] T031 — `source/Testurio.Web/e2e/account-settings.spec.ts` (E2E tests for navigation, header refresh on profile save, language switching, dark mode toggle)

**Acceptance Criteria Coverage:**

- Backend ACs (AC-029 to AC-039): ✓ Tests exist but cannot build
- Backend ACs (AC-040 to AC-043): ⚠️ Schema requirements (handled by CosmosDbInitializer, tested indirectly)
- Frontend ACs (AC-001 to AC-028): Partially covered — 1 failing test (AC-028), 0 E2E coverage for full user flows

**Blockers:**

1. One frontend test is failing (AC-028) — this must be fixed before Test phase can be marked complete
2. E2E tests not implemented (T031) — while not strictly required for basic test phase, they provide full-stack coverage
3. Backend test suite cannot compile due to unrelated issues in pipeline executor tests

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
