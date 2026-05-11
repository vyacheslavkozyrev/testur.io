# User Stories — Project Dashboard — Real-Time Updates (0043)

## Prerequisites

Feature 0010 (Project Dashboard — Snapshot) must be complete. This feature extends the dashboard page component, the stats endpoints file, and the DI registration introduced by feature 0010.

## Out of Scope

The following are explicitly **not** part of this feature:

- The initial snapshot fetch (`GET /v1/stats/dashboard`) — owned by feature 0010
- Per-project history real-time updates — feature 0011 is responsible if it requires live data
- Push notifications or email alerts when a run completes

---

## Stories

### US-001: Automatic Run Status Badge Updates via SSE

**As a** QA lead with the dashboard open
**I want to** see run status badges update automatically as test runs progress
**So that** I do not need to manually refresh the page to know whether a run has passed or failed

#### Acceptance Criteria

- [ ] AC-001: The frontend opens a persistent SSE connection to `GET /v1/stats/dashboard/stream` immediately after the initial dashboard snapshot is loaded
- [ ] AC-002: The SSE endpoint pushes a `DashboardUpdatedEvent` JSON payload whenever a run status changes for any project belonging to the authenticated user; the payload contains at minimum: `projectId`, `latestRun` (with `runId`, `status`, `startedAt`, `completedAt`)
- [ ] AC-003: On receiving a `DashboardUpdatedEvent`, the React state for the affected project card is updated in place — the correct card's badge and timestamp reflect the new status without a full page reload or re-fetch
- [ ] AC-004: If the SSE connection drops (network interruption, server restart), the client attempts to reconnect with exponential back-off (initial delay 1 s, max delay 30 s, max 5 attempts); the user sees a subtle "Reconnecting…" indicator during this period
- [ ] AC-005: If all reconnect attempts fail, the client falls back to a one-time re-fetch of `GET /v1/stats/dashboard` and stops attempting SSE; the user sees a "Live updates unavailable — data may be stale" warning
- [ ] AC-006: The SSE connection is closed and cleaned up when the user navigates away from `/dashboard`
- [ ] AC-007: A `DashboardUpdatedEvent` for a `projectId` not currently in the card list (e.g. a project added in another tab) triggers a full snapshot re-fetch rather than attempting to insert a card mid-stream

#### Edge Cases

- Two browser tabs open for the same user each maintain independent SSE connections; both receive the same events
- The SSE connection must not be re-opened if the component is still mounted and the connection is healthy

---

### US-002: Quota Usage Updates via SSE

**As a** QA lead watching a test run start
**I want to** see the quota counter increment in real time as runs are triggered
**So that** I do not need to reload the page to know how close I am to the daily limit

#### Acceptance Criteria

- [ ] AC-008: When a `DashboardUpdatedEvent` payload includes a `quotaUsage` field, the `QuotaUsageBar` component updates its displayed values in place without a full re-fetch
- [ ] AC-009: When a `DashboardUpdatedEvent` payload omits `quotaUsage`, the `QuotaUsageBar` retains the value from the last snapshot fetch or the last SSE event that carried quota data

#### Edge Cases

- A quota increment SSE event received while SSE is in reconnect state is applied once the connection is restored and the snapshot re-fetch completes (the re-fetch carries the latest quota)

---

### US-003: SSE Security and Data Isolation

**As a** QA lead
**I want to** be confident that the SSE stream only delivers events belonging to my account
**So that** no other user's test results are ever pushed to my browser

#### Acceptance Criteria

- [ ] AC-010: `GET /v1/stats/dashboard/stream` requires a valid Azure AD B2C JWT; a missing or invalid token returns `401 Unauthorized` and the stream is not opened
- [ ] AC-011: The SSE endpoint only pushes `DashboardUpdatedEvent` items for projects belonging to the authenticated user's `userId`; events for other users are never sent on the same connection
- [ ] AC-012: The API fans out SSE events by consuming Service Bus messages posted by the Worker when a run status changes; the Worker does not call the SSE endpoint directly
- [ ] AC-013: The `userId` extracted from the JWT is used to scope which `Channel<DashboardUpdatedEvent>` the connection reads from; no cross-tenant channel reads are possible

#### Edge Cases

- A request with a structurally valid but expired JWT receives `401 Unauthorized`

---

### US-004: Reconnect Indicator UI

**As a** QA lead
**I want to** see clear feedback when the live update connection is degraded or lost
**So that** I know whether badge data on screen is current

#### Acceptance Criteria

- [ ] AC-014: While the client is in exponential back-off reconnect mode, a subtle non-blocking indicator is shown (e.g. a chip or snackbar reading "Reconnecting…")
- [ ] AC-015: Once the connection is restored, the "Reconnecting…" indicator disappears and no further message is shown
- [ ] AC-016: When all reconnect attempts have been exhausted and the fallback re-fetch has completed, a persistent non-blocking warning reading "Live updates unavailable — data may be stale" is shown until the user manually refreshes
- [ ] AC-017: Neither indicator blocks interaction with project cards, the quota bar, or the "Create Project" button

#### Edge Cases

- If the connection drops and restores within the first back-off interval, no indicator is shown to the user (debounce to avoid flicker on transient drops)
