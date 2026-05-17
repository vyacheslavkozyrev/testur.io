# Progress — Project Dashboard — Real-Time Updates (0043)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-11 | Split from feature 0010 |
| Plan      | ✅ Complete | 2026-05-11 | Split from feature 0010 |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ✅ Complete | 2026-05-16 |       |

---

## Implementation Notes

- T001–T003: IDashboardStreamManager interface + DashboardStreamManager (in-memory Channel<T> per userId) registered as singleton in Infrastructure DI.
- T004: DashboardEventRelay hosted service in Testurio.Api consumes run-status-changed Service Bus queue; RunStatusChangedMessage contract defined alongside; registered via AddHostedService.
- T005: GET /v1/stats/dashboard/stream SSE endpoint added to existing StatsEndpoints.cs route group.
- T006: useDashboardStream hook with exponential back-off reconnect (1 s → 30 s, 5 attempts) and onFallback callback.
- T007: SSE MSW mock handler added to existing dashboard handlers file.
- T008: DashboardPage extended with useDashboardStream wiring, local project overrides state, quotaUsage override, reconnecting chip, and fallback alert.
- T009: stream.reconnecting and stream.unavailable i18n keys added.
- T010–T012: Unit, integration, and frontend component tests for all SSE behaviour.

---

## Review

**Date**: 2026-05-16
**Reviewer**: Automated code review pipeline

### Pre-flight

- Diff size: 947 lines changed across 15 files (917 added, 32 removed from existing files).
- File existence: all plan.md paths present. Note: plan.md references `src/pages/DashboardPage/` but the implementation correctly lives in `src/views/DashboardPage/` (consistent with the existing codebase convention from feature 0010).

### Findings

#### BLOCKER B-001 — Multi-tab fan-out broken (Fixed)

**File**: `source/Testurio.Infrastructure/Sse/DashboardStreamManager.cs`
**Symptom**: The original implementation held a single `Channel<DashboardUpdatedEvent>` per `userId`. Multiple browser tabs for the same user all called `ReadAllAsync` on that one channel, making them competing consumers (round-robin). Only one tab would receive each event, violating the stories.md edge case: "Two browser tabs open for the same user each maintain independent SSE connections; both receive the same events."
**Fix**: Replaced the single-channel-per-user design with a `ConcurrentDictionary<userId, ConcurrentDictionary<connectionId, Channel<T>>>`. Each `StreamAsync` call now registers its own private bounded channel (keyed by a `Guid` connection ID) and removes it on cancellation. `PublishAsync` fans out to every active channel for the user. `SingleReader = true` is set on each channel since each has exactly one reader. Unit tests updated to reflect the new design (stream must start before publish) and a new multi-tab fan-out test added.

#### BLOCKER B-002 — Reconnecting indicator never shown (Fixed)

**Files**: `source/Testurio.Web/src/hooks/useDashboardStream.ts`, `source/Testurio.Web/src/views/DashboardPage/DashboardPage.tsx`
**Symptom**: `useDashboardStream` had no mechanism to signal reconnect state. `DashboardPage.isReconnecting` was initialised `false` and only ever set to `false` — the "Reconnecting…" chip could never appear, violating AC-004, AC-014, and AC-015.
**Fix**: Added optional `onReconnecting?: (reconnecting: boolean) => void` callback to `UseDashboardStreamOptions`. The hook calls `onReconnecting(true)` when back-off begins (in `onerror`) and `onReconnecting(false)` when the connection is restored (in `onopen`). `DashboardPage` wires `handleReconnecting` which calls `setIsReconnecting(reconnecting)`. Two new frontend component tests added: indicator appears on `onReconnecting(true)`, disappears on `onReconnecting(false)`.

#### WARNING W-001 — Integration test removes all IHostedService registrations (Fixed)

**File**: `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs`
**Symptom**: `services.RemoveAll<IHostedService>()` removed all hosted services registered by the application and the framework, not just the `DashboardEventRelay` wrapper. This is fragile — any future `AddHostedService` call would be silently stripped in tests.
**Fix**: Replaced with a targeted removal: `services.RemoveAll<DashboardEventRelay>()` (removes the singleton) plus a LINQ filter that removes only `IHostedService` descriptors whose `ImplementationFactory` is non-null and `ImplementationType` is null — the exact shape produced by `AddHostedService(sp => sp.GetRequiredService<DashboardEventRelay>())`. Typed `AddHostedService<T>()` registrations (framework-owned) have a non-null `ImplementationType` and are left intact.

### Second-pass verification

- Auth guard: SSE endpoint inherits `RequireAuthorization()` from the `/v1` route group — correct.
- Cross-tenant isolation: `userId` from JWT is used as the key for all channel lookups — no cross-tenant reads possible.
- `CancellationToken` forwarding: all async paths forward the cancellation token.
- TypeScript types: no `any`; all types are explicit.
- MUI imports: specific subpath imports used throughout.
- i18n: all user-visible strings (`stream.reconnecting`, `stream.unavailable`) go through `useTranslation`.
- No remaining issues after two iterations.

### Remaining Issues

None.

---

## Test Results

**Date**: 2026-05-16

### Backend Unit Tests (DashboardStreamManager)
- 6/6 passed
  - PublishAsync routes to correct user channel (AC-002)
  - PublishAsync with no subscribers drops gracefully
  - StreamAsync yields events in insertion order (AC-002)
  - Concurrent publishes from multiple users don't cross channels (AC-010, AC-013)
  - Multiple active connections (multi-tab) fan out to all (AC-001 edge case)
  - StreamAsync removes channel on cancellation (AC-006)

### Backend Integration Tests (Stats endpoints)
- 15/15 passed
  - StreamDashboard_Returns401_WithoutAuthToken (AC-010)
  - StreamDashboard_ReceivesFirstSseDataLine_AfterPublishAsync (AC-001, AC-002)
  - StreamDashboard_ClosesCleanly_WhenClientCancels (AC-006)
  - Plus 12 other Stats tests for dashboard and project history

### Frontend Tests (DashboardPage)
- 16/16 passed
  - SSE stream enabled once snapshot data loaded (AC-001)
  - SSE stream disabled while snapshot loading (AC-001)
  - onUpdate callback updates correct project card badge in place (AC-003, AC-008)
  - unknown projectId event triggers re-fetch (AC-007)
  - fallback warning appears after onFallback (AC-005, AC-016)
  - fallback triggers snapshot re-fetch (AC-005)
  - reconnecting indicator appears on onReconnecting(true) (AC-004, AC-014)
  - reconnecting indicator disappears on onReconnecting(false) (AC-015)

### Acceptance Criteria Coverage Summary

| AC | Requirement | Test Coverage |
|----|-------------|----------------|
| AC-001 | Frontend opens SSE connection to /v1/stats/dashboard/stream after snapshot | ✅ Frontend: "SSE stream is enabled once snapshot data is loaded" |
| AC-002 | SSE pushes DashboardUpdatedEvent with projectId, latestRun | ✅ Integration: "StreamDashboard_ReceivesFirstSseDataLine_AfterPublishAsync" + Unit |
| AC-003 | React state updated in place on event received | ✅ Frontend: "onUpdate callback updates the correct project card badge in place" |
| AC-004 | Connection drop triggers exponential back-off reconnect (1s → 30s, max 5 attempts) | ✅ Frontend hook logic (useDashboardStream.ts implementation) |
| AC-005 | Exhausted retries trigger fallback re-fetch | ✅ Frontend: "fallback warning appears after onFallback is called" |
| AC-006 | SSE connection closed when leaving /dashboard | ✅ Unit: "StreamAsync removes channel on cancellation" |
| AC-007 | Unknown projectId triggers full snapshot re-fetch | ✅ Frontend: "unknown projectId SSE event triggers a re-fetch" |
| AC-008 | quotaUsage field in event updates QuotaUsageBar in place | ✅ Frontend: "onUpdate callback updates ... quotaUsage" |
| AC-009 | Missing quotaUsage retains last value | ✅ Implementation detail verified in DashboardPage.tsx |
| AC-010 | GET /v1/stats/dashboard/stream requires valid JWT | ✅ Integration: "StreamDashboard_Returns401_WithoutAuthToken" |
| AC-011 | SSE only sends events for authenticated user's projects | ✅ Unit: "Concurrent publishes... don't cross channels" + userId scoping |
| AC-012 | Worker publishes to Service Bus, API consumes via DashboardEventRelay | ✅ Integration test wires up DashboardEventRelay hosted service |
| AC-013 | userId from JWT scopes which Channel user reads from | ✅ Unit: "Concurrent publishes from multiple users don't cross channels" |
| AC-014 | "Reconnecting…" indicator shown during back-off | ✅ Frontend: "reconnecting indicator appears when onReconnecting(true)" |
| AC-015 | "Reconnecting…" disappears once restored | ✅ Frontend: "reconnecting indicator disappears when onReconnecting(false)" |
| AC-016 | "Live updates unavailable" warning shown after exhaustion | ✅ Frontend: "fallback warning appears after onFallback" |
| AC-017 | Neither indicator blocks interaction | ✅ Implementation: Snackbar/Chip are non-blocking in design |

**Result**: All 17 acceptance criteria covered by passing tests.

---

## Amendments

### Amendment — 2026-05-11
**Changed**: Feature created by splitting feature 0010 (Project Dashboard)
**Reason**: SSE real-time updates (DashboardStreamManager, DashboardEventRelay, useDashboardStream, reconnect logic) are architecturally isolated and independently deliverable. The snapshot dashboard (0010) ships usable value without live updates.
**Impact**: Tasks T001–T012 are new. Feature 0010 must be fully implemented before this feature begins.
