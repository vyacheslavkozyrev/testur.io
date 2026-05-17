# Progress — Project Dashboard — Real-Time Updates (0043)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-11 | Split from feature 0010 |
| Plan      | ✅ Complete | 2026-05-11 | Split from feature 0010 |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ⏳ Pending  |            |       |

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

_Populated by `/test [####]`_

---

## Amendments

### Amendment — 2026-05-11
**Changed**: Feature created by splitting feature 0010 (Project Dashboard)
**Reason**: SSE real-time updates (DashboardStreamManager, DashboardEventRelay, useDashboardStream, reconnect logic) are architecturally isolated and independently deliverable. The snapshot dashboard (0010) ships usable value without live updates.
**Impact**: Tasks T001–T012 are new. Feature 0010 must be fully implemented before this feature begins.
