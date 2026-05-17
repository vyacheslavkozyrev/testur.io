# Implementation Plan — Project Dashboard — Real-Time Updates (0043)

## Prerequisite

Feature 0010 must be complete. The following artifacts from feature 0010 are used here and must not be duplicated:
- `Testurio.Core/Events/DashboardUpdatedEvent.cs`
- `Testurio.Core/Interfaces/IStatsRepository.cs`
- `Testurio.Api/Endpoints/StatsEndpoints.cs` (extended, not replaced)
- `Testurio.Infrastructure/DependencyInjection.cs` (extended)
- `source/Testurio.Web/src/types/dashboard.types.ts` (DashboardUpdatedEvent type already defined)
- `source/Testurio.Web/src/mocks/handlers/dashboard.ts` (extended)
- `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.tsx` (extended)
- `source/Testurio.Web/src/locales/en/dashboard.json` (extended)

## Tasks

- [x] T001 [Domain] Add `IDashboardStreamManager` interface with methods `PublishAsync(userId, DashboardUpdatedEvent)` and `StreamAsync(userId, CancellationToken)` — `source/Testurio.Core/Interfaces/IDashboardStreamManager.cs`
- [x] T002 [Infra] Implement `DashboardStreamManager`: holds per-userId `Channel<DashboardUpdatedEvent>` instances; `PublishAsync` writes to the matching channel (creates it if it does not exist); `StreamAsync` reads from the channel and yields `DashboardUpdatedEvent` items — `source/Testurio.Infrastructure/Sse/DashboardStreamManager.cs`
- [x] T003 [Infra] Register `DashboardStreamManager` as a singleton in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T004 [App] Implement `DashboardEventRelay` hosted service: subscribes to the Service Bus topic for run-status-changed messages; for each message deserialises a `DashboardUpdatedEvent` and calls `IDashboardStreamManager.PublishAsync(userId, event)` — `source/Testurio.Api/Services/DashboardEventRelay.cs`; verify the Worker message contract includes `userId`, `projectId`, and `RunStatus` before coding the deserialiser
- [x] T005 [API] Add `GET /v1/stats/dashboard/stream` SSE endpoint to the existing stats route group: requires auth, sets `Content-Type: text/event-stream`, calls `IDashboardStreamManager.StreamAsync(userId, ct)` and writes each `DashboardUpdatedEvent` as a `data:` line — `source/Testurio.Api/Endpoints/StatsEndpoints.cs`
- [x] T006 [UI] Add `useDashboardStream` custom hook: opens `EventSource` to `/v1/stats/dashboard/stream` after snapshot is loaded; on `message` event parses `DashboardUpdatedEvent` and calls provided `onUpdate(event)` callback; implements exponential back-off reconnect (initial 1 s, max 30 s, max 5 attempts); on exhaustion calls provided `onFallback()` callback and stops reconnecting; closes `EventSource` on unmount — `source/Testurio.Web/src/hooks/useDashboardStream.ts`
- [x] T007 [UI] Add SSE mock handler for `GET /v1/stats/dashboard/stream` to the existing dashboard handlers file — `source/Testurio.Web/src/mocks/handlers/dashboard.ts`
- [x] T008 [UI] Extend `DashboardPage`: import `useDashboardStream`; on `onUpdate` callback update the affected project card in local state and optionally refresh `quotaUsage`; on unknown `projectId` trigger a `useDashboard` re-fetch; wire reconnect state to show "Reconnecting…" indicator and fallback state to show "Live updates unavailable" warning — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.tsx`
- [x] T009 [UI] Add SSE-related translation keys to dashboard locale file: `stream.reconnecting`, `stream.unavailable` — `source/Testurio.Web/src/locales/en/dashboard.json`
- [x] T010 [Test] Backend unit tests for `DashboardStreamManager`: `PublishAsync` routes to the correct channel, unknown `userId` creates a new channel on first publish, `StreamAsync` yields events in insertion order, concurrent publishes from multiple users do not cross channels — `tests/Testurio.UnitTests/Services/DashboardStreamManagerTests.cs`
- [x] T011 [Test] Backend integration tests for `GET /v1/stats/dashboard/stream`: auth required (401 on missing token), first SSE `data:` line received after `PublishAsync`, connection closes cleanly on cancellation — `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs`
- [x] T012 [Test] Frontend component tests for `DashboardPage` SSE behaviour: `useDashboardStream` callback updates the correct card badge in place, unknown `projectId` event triggers re-fetch, reconnecting indicator appears on connection drop, fallback warning appears after all reconnect attempts fail — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.test.tsx`

## Rationale

**Domain interface first (T001).** `IDashboardStreamManager` depends on `DashboardUpdatedEvent`, which is already defined in feature 0010's domain layer. The interface is defined before the implementation so the DI container and tests can reference it.

**`DashboardStreamManager` as a singleton (T002–T003).** The channel map must survive the lifetime of individual HTTP requests — it holds open connections across requests. A scoped or transient registration would create a new map per request, making fan-out impossible.

**`DashboardEventRelay` as a hosted service in `Testurio.Api` (T004).** The Worker already publishes run-status-changed messages to Service Bus as part of the test pipeline (feature 0001). `DashboardEventRelay` consumes those messages and calls `IDashboardStreamManager.PublishAsync`. This keeps the Worker's scope limited to test execution and result writing; real-time delivery belongs in the API process where SSE connections are held. Before coding the deserialiser, verify that the Worker message schema (from feature 0001) includes `userId`, `projectId`, and `RunStatus` — if not, an amendment to feature 0001 is required.

**SSE endpoint added to existing `StatsEndpoints.cs` (T005).** Keeping both stats endpoints in one file preserves the route group cohesion established by feature 0010.

**`useDashboardStream` as a dedicated hook (T006).** SSE connection lifecycle (open, reconnect with back-off, close on unmount, fallback to re-fetch) is non-trivial and must be isolated from `DashboardPage` to be testable in isolation. The hook accepts `onUpdate` and `onFallback` callbacks so `DashboardPage` controls state mutations and the hook remains stateless.

**`DashboardPage` is extended, not replaced (T008).** Feature 0010 created the component; this feature adds the `useDashboardStream` wiring. No files from feature 0010 are deleted or replaced — only appended to.

**Tests last (T010–T012).** `DashboardStreamManager` unit tests (T010) mock nothing — they test the in-memory channel directly. Integration tests (T011) require a fully wired container and a Service Bus test double (or a direct call to `PublishAsync` bypassing the relay). Frontend tests (T012) require the SSE MSW handler added in T007.

**Cross-feature dependencies.**

- **Feature 0001**: Provides the Service Bus message contract consumed by `DashboardEventRelay` (T004). The `userId`, `projectId`, and `RunStatus` fields must be present in the message payload; confirm before implementing T004.
- **Feature 0010**: Provides `DashboardUpdatedEvent`, `IDashboardStreamManager`'s prerequisite value objects, `StatsEndpoints.cs`, `DashboardPage`, `dashboard.types.ts`, and the dashboard MSW handler file — all extended here.
- **Feature 0021** (Plan-Tier Quota Enforcement): May post `quotaUsage` increments in run-status-changed messages. If so, `DashboardEventRelay` propagates them through `DashboardUpdatedEvent.quotaUsage` and `DashboardPage` updates the `QuotaUsageBar` in real time.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys |
| `[Test]` | Unit, integration, and frontend component test files |
