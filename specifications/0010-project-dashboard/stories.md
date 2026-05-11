# User Stories — Project Dashboard — Snapshot (0010)

## Out of Scope

The following are explicitly **not** part of this feature:

- Real-time SSE run status updates — covered by feature 0043
- Per-project test history content and trend charts — covered by feature 0011
- The Settings button on the per-project history page — feature 0011 owns and implements it; feature 0010 only defines the navigation route constant it targets
- Plan-tier quota enforcement logic — the dashboard displays quota usage but enforcement is covered by feature 0021
- Cross-project aggregate statistics (e.g. total pass rate across all projects)
- Filtering or searching of project cards
- Notifications or alerts sent outside the dashboard UI

---

## Stories

### US-001: Dashboard Card Grid

**As a** QA lead
**I want to** see all my projects displayed as cards sorted by most recent activity, with a rich run status badge on each card
**So that** I can immediately identify which products need attention without opening each project individually

#### Acceptance Criteria

- [ ] AC-001: The dashboard page is accessible at `/dashboard` and is the default authenticated landing page
- [ ] AC-002: Each project card displays: project name, product URL, testing strategy, run status badge, and the `startedAt` timestamp of the most recent run (or "Never run" when no runs exist)
- [ ] AC-003: Cards are sorted server-side by `latestRun.startedAt` descending — projects with at least one run appear first; projects with no runs appear last, sorted alphabetically by name within that group
- [ ] AC-004: The run status badge renders one of exactly seven statuses with distinct visual treatments: `queued` (grey), `running` (amber, animated), `passed` (green), `failed` (red), `cancelled` (neutral/dark), `timed-out` (orange), `never_run` (muted — displayed when `latestRun` is null)
- [ ] AC-005: A loading skeleton matching the card grid layout is shown while the dashboard data is being fetched
- [ ] AC-006: If the API request fails, an inline error state is shown with a "Retry" action that re-triggers the fetch
- [ ] AC-007: A "Create Project" button is always visible in the dashboard header regardless of how many projects exist; it navigates to the project creation flow (feature 0006)
- [ ] AC-008: The dashboard fetches data from `GET /v1/stats/dashboard`; the response is the single authoritative source for card content — no secondary requests are made per card

#### Edge Cases

- A project whose most recent run has status `running` shows an animated badge; the badge updates when the user manually re-fetches or when feature 0043 SSE updates are wired in
- A project with `latestRun: null` renders the `never_run` badge and shows no timestamp
- If the API returns an empty projects array (user has no projects), the empty state (US-002) is displayed instead of the card grid

---

### US-002: Empty State — No Projects CTA

**As a** QA lead who has just created an account
**I want to** see a clear call to action when I have no projects yet
**So that** I know how to get started without being confused by a blank screen

#### Acceptance Criteria

- [ ] AC-009: When `GET /v1/stats/dashboard` returns an empty projects array, the card grid is replaced by a centred empty state panel containing: an illustrative icon, a heading, a supporting description, and a "Create your first project" button
- [ ] AC-010: The "Create your first project" button opens the project creation flow (feature 0006) — it does not redirect away from `/dashboard`; the creation flow is triggered in place (e.g. a modal or a navigation to the creation page that returns to `/dashboard` on completion)
- [ ] AC-011: The empty state panel is not shown when a fetch is in progress or in an error state — only when a successful response confirms zero projects
- [ ] AC-012: The "Create Project" button in the dashboard header (AC-007) remains visible in the empty state

#### Edge Cases

- A user who deletes their last project sees the empty state on their next dashboard load
- The empty state must not flash briefly before the loading skeleton disappears — show skeleton first, then empty state

---

### US-003: Global Quota Usage Display

**As a** QA lead
**I want to** see my plan's test run quota usage at the top of the dashboard
**So that** I can avoid being surprised when triggers are rejected due to quota exhaustion

#### Acceptance Criteria

- [ ] AC-013: A quota usage bar or indicator is displayed globally at the top of the dashboard page, above the card grid; it is not embedded inside individual project cards
- [ ] AC-014: The indicator shows: "X / Y runs used today" where X is `usedToday` and Y is `dailyLimit`, plus a `resetsAt` label ("Resets at midnight UTC" or a formatted local time equivalent)
- [ ] AC-015: The `GET /v1/stats/dashboard` response includes a `quotaUsage` object with fields: `usedToday` (integer), `dailyLimit` (integer), `resetsAt` (ISO 8601 UTC timestamp for next midnight UTC)
- [ ] AC-016: When `usedToday` equals `dailyLimit`, the indicator is highlighted in amber to warn the user; when `usedToday` exceeds `dailyLimit` the indicator is highlighted in red (defensive — quota enforcement by feature 0021 should prevent this)
- [ ] AC-017: When no active subscription plan exists, `dailyLimit` is `0` and the indicator displays "No active plan" instead of a numeric ratio
- [ ] AC-018: The quota counter reflects only runs triggered since midnight UTC on the current calendar day; runs from prior days are excluded
- [ ] AC-019: The quota indicator reflects the value from the last successful snapshot fetch; real-time quota increments via SSE are handled by feature 0043

#### Edge Cases

- A user on a free tier with `dailyLimit: 0` sees "No active plan" — not "0 / 0 runs"
- The `resetsAt` timestamp must always be in the future; the server computes it as `next midnight UTC` relative to the time the snapshot request is processed

---

### US-004: Card Navigation to Per-Project History Page

**As a** QA lead
**I want to** click a project card to open that project's test history
**So that** I can investigate individual run results without leaving the main workflow

#### Acceptance Criteria

- [ ] AC-020: Clicking anywhere on a project card navigates to the per-project history page at `/projects/:id/history` where `:id` is the `projectId`
- [ ] AC-021: The route path `/projects/:id/history` is defined as a named route constant in `source/Testurio.Web/src/routes/routes.tsx` so that feature 0011 can import and own the page component registered at that path
- [ ] AC-022: The card click target covers the entire card surface; no sub-element (e.g. the status badge) intercepts or cancels the click
- [ ] AC-023: The browser back button from `/projects/:id/history` returns the user to `/dashboard` and restores the previous scroll position
- [ ] AC-024: Card navigation uses client-side routing (Next.js `Link` or `router.push`) — not a full page reload

#### Edge Cases

- If a project is deleted between the snapshot load and the card click, the history page at `/projects/:id/history` handles the 404 response from its own API call (feature 0011's responsibility); feature 0010 only performs the navigation

---

### US-005: Navigation Contract for Settings Button (Feature 0011 Dependency)

**As a** feature 0011 implementer
**I want** a defined route constant for the project settings page
**So that** the Settings button on the per-project history page can link to the correct URL without duplicating the route definition

#### Acceptance Criteria

- [ ] AC-025: `source/Testurio.Web/src/routes/routes.tsx` exports a named constant `PROJECT_SETTINGS_ROUTE` with value `/projects/:id/settings` in addition to the history route constant
- [ ] AC-026: The route constant is exported as a plain string template or a builder function `(id: string) => string` so that feature 0011 can construct the concrete URL at render time
- [ ] AC-027: Feature 0010 does NOT register a page component at `/projects/:id/settings` — the route constant is a navigation target only; the settings page component is owned by the feature that implements project configuration (feature 0006 or its amendment)

#### Edge Cases

- If feature 0006 already exports a settings route constant, `PROJECT_SETTINGS_ROUTE` in feature 0010's routes file must be aligned with or re-exported from feature 0006's definition to avoid two different path strings for the same destination

---

### US-006: Dashboard API Security and Data Isolation

**As a** QA lead
**I want to** be confident that the dashboard only shows data belonging to my account
**So that** no information from other users is ever visible to me

#### Acceptance Criteria

- [ ] AC-028: `GET /v1/stats/dashboard` requires a valid Azure AD B2C JWT; a missing or invalid token returns `401 Unauthorized`
- [ ] AC-029: The `userId` extracted from the JWT is used as the Cosmos DB partition key for all queries; no query is issued without this scope
- [ ] AC-030: The snapshot endpoint never returns projects, run results, or quota data belonging to a different `userId`
- [ ] AC-031: Soft-deleted projects (`isDeleted: true`) are excluded from both the snapshot response
- [ ] AC-032: The snapshot endpoint returns `200 OK` with an empty `projects` array when the authenticated user has no active projects

#### Edge Cases

- A request with a structurally valid but expired JWT receives `401 Unauthorized`
- A request with a valid JWT for user A that attempts to pass a different `userId` as a query parameter is ignored — the server always derives `userId` from the token, never from caller-supplied input
