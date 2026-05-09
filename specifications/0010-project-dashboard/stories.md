# User Stories — Project Dashboard (0010)

## Out of Scope

The following are explicitly **not** part of this feature:

- Full per-project test history with trend charts — covered by feature 0011
- Individual test run report detail view — covered by feature 0011
- Plan-tier quota progress bar — the dashboard shows quota usage but quota enforcement logic is covered by feature 0021
- Real-time push updates (WebSocket / SSE) — the dashboard polls or refreshes on navigation; live streaming is not required for MVP
- Filtering or sorting of project cards — all projects are displayed in creation-order descending
- Cross-project aggregate statistics (e.g. total pass rate across all projects) — not in scope
- Notifications or alerts sent outside the dashboard UI — covered by separate notification features
- Project creation from the dashboard beyond the "Create Project" entry-point CTA — form and logic are in feature 0006

---

## Stories

### US-001: View All Projects at a Glance

**As a** QA lead
**I want to** see all my projects listed on a single dashboard screen with their most recent test run status
**So that** I can quickly identify which products need attention without opening each project individually

#### Acceptance Criteria

- [ ] AC-001: The Dashboard page is the default authenticated landing page and is accessible at `/dashboard`
- [ ] AC-002: Each project is displayed as a card showing: project name, product URL, testing strategy, most recent run status badge, and the timestamp of the most recent run
- [ ] AC-003: The run status badge uses distinct visual treatments for each status: Passed (green), Failed (red), Running (amber/animated), Queued (grey), and None (neutral — no run yet)
- [ ] AC-004: Projects are listed in descending order of `createdAt` (most recently created first)
- [ ] AC-005: If the authenticated user has no projects, an empty state is displayed with a "Create your first project" call-to-action that navigates to the project creation flow (feature 0006)
- [ ] AC-006: The dashboard fetches data from `GET /v1/stats/dashboard` and renders within 2 seconds on a standard connection
- [ ] AC-007: A loading skeleton is shown while the dashboard data is being fetched
- [ ] AC-008: If the API request fails, an error state is shown with a "Retry" action that re-triggers the fetch

---

### US-002: Navigate to a Project from the Dashboard

**As a** QA lead
**I want to** click on a project card to open that project's detail or settings view
**So that** I can investigate a failure or adjust configuration without leaving the dashboard flow

#### Acceptance Criteria

- [ ] AC-009: Clicking anywhere on a project card (except the status badge tooltip) navigates to the project's settings page at `/projects/{projectId}/settings`
- [ ] AC-010: A "Create Project" button is always visible in the dashboard header regardless of how many projects exist
- [ ] AC-011: The dashboard retains its scroll position when the user navigates back from a project detail page (browser back-button behaviour)

---

### US-003: See the Most Recent Run Status per Project

**As a** QA lead
**I want to** see the outcome of the latest test run for each project without opening individual run reports
**So that** I can immediately spot failures and prioritise where to investigate

#### Acceptance Criteria

- [ ] AC-012: The `GET /v1/stats/dashboard` endpoint returns for each project: `projectId`, `name`, `productUrl`, `testingStrategy`, `createdAt`, `latestRun` (nullable object containing `runId`, `status`, `startedAt`, `completedAt`)
- [ ] AC-013: `latestRun` is `null` for projects that have never had a test run
- [ ] AC-014: The run status reflects the most recently started run, determined by `startedAt` descending
- [ ] AC-015: Run status values returned by the API are: `Passed`, `Failed`, `Running`, `Queued`
- [ ] AC-016: The endpoint only returns projects belonging to the authenticated user's `userId`; no cross-tenant data is ever included
- [ ] AC-017: Soft-deleted projects (`isDeleted: true`) are excluded from the dashboard response
- [ ] AC-018: The endpoint returns `200 OK` with an empty array when the user has no active projects

---

### US-004: View Current Test Run Quota Usage

**As a** QA lead
**I want to** see how many test runs I have used today against my plan's daily limit
**So that** I can avoid being surprised when triggers are rejected due to quota exhaustion (feature 0021)

#### Acceptance Criteria

- [ ] AC-019: The dashboard header displays a quota usage indicator: "X / Y runs used today" where X is runs triggered since midnight UTC and Y is the plan's daily limit
- [ ] AC-020: The `GET /v1/stats/dashboard` response includes a `quotaUsage` object with fields: `usedToday` (integer), `dailyLimit` (integer), `resetsAt` (ISO 8601 UTC timestamp for next midnight)
- [ ] AC-021: When `usedToday` equals `dailyLimit`, the quota indicator is highlighted in amber to warn the QA lead
- [ ] AC-022: When no subscription plan is active, `dailyLimit` is `0` and the indicator shows "No active plan" instead of a numeric ratio
- [ ] AC-023: The quota counter reflects only runs triggered within the current calendar day (UTC); runs from previous days are not counted

---

### US-005: Dashboard API Security and Data Isolation

**As a** QA lead
**I want to** be confident that the dashboard only shows data belonging to my account
**So that** no information from other users is ever visible to me

#### Acceptance Criteria

- [ ] AC-024: `GET /v1/stats/dashboard` requires a valid Azure AD B2C JWT; a missing or invalid token returns `401 Unauthorized`
- [ ] AC-025: The `userId` extracted from the JWT is used as the Cosmos DB partition key for all queries; no query is issued without this scope
- [ ] AC-026: The endpoint never returns projects, run results, or quota data belonging to a different `userId`
