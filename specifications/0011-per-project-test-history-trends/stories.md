# User Stories — Per-Project Test History & Trends (0011)

## Out of Scope

The following are explicitly **not** part of this feature:

- Filtering the run history list by date range — only the trend chart uses date range selection; the list always shows all runs
- Exporting run history to CSV or PDF
- Comparing two specific runs side by side
- Deleting individual runs
- Real-time status badge updates during an active run — covered by feature 0043
- Pagination of the run history list — all runs for a project are loaded in a single request

---

## Stories

### US-001: Run History List

**As a** QA lead  
**I want to** see all test runs for a project displayed as a table ordered by most recent first  
**So that** I can review the full testing history at a glance

#### Acceptance Criteria

- [ ] AC-001: The history page is accessible at `/projects/:id/history` (route constant `PROJECT_HISTORY_ROUTE` defined in feature 0010)
- [ ] AC-002: The page fetches data from `GET /v1/stats/projects/{projectId}/history`; the response is the single authoritative source for both the history table and the trend chart
- [ ] AC-003: Each row in the history table displays: story title, verdict badge, `createdAt` formatted as a human-readable local date and time, total duration in seconds, and a scenario pass/fail count rendered as `X passed / Y total`
- [ ] AC-004: Rows are sorted server-side by `createdAt` descending (most recent first)
- [ ] AC-005: The verdict badge reuses the `RunStatusBadge` component from feature 0010, mapping `"PASSED"` to the `passed` status and `"FAILED"` to the `failed` status
- [ ] AC-006: Clicking a row opens the run detail panel (US-003) without navigating away from the history page; only one panel can be open at a time
- [ ] AC-007: A loading skeleton matching the table column layout is displayed while the fetch is in progress
- [ ] AC-008: If the API request fails, an inline error state is shown with a "Retry" action that re-triggers the fetch
- [ ] AC-009: When the project exists but has no runs, an empty state panel is shown with the message "No test runs yet"
- [ ] AC-010: A "Project Settings" button in the page header navigates to `/projects/:id/settings` using `PROJECT_SETTINGS_ROUTE` from `routes.tsx`

#### Edge Cases

- If the project does not exist or belongs to a different user, `GET /v1/stats/projects/{projectId}/history` returns `404 Not Found` and the page renders a project-not-found error state
- A project with hundreds of runs loads all of them in a single response — no pagination is applied

---

### US-002: Pass/Fail Trend Chart

**As a** QA lead  
**I want to** view a daily pass/fail trend chart with selectable fixed time ranges  
**So that** I can identify recurring failure patterns and quality trends over time without manual counting

#### Acceptance Criteria

- [ ] AC-011: The trend chart is displayed above the run history table on the history page
- [ ] AC-012: Three time-range toggle buttons are displayed: **Last 7 days**, **Last 30 days**, **Last 90 days**; the default selection on page load is **Last 30 days**
- [ ] AC-013: The `GET /v1/stats/projects/{projectId}/history` response includes a `trendPoints` array of daily buckets — `{ date: string (ISO 8601 date), passed: number, failed: number }` — spanning a 90-day window ending today (server-computed, UTC)
- [ ] AC-014: When a time range button is selected, the chart filters `trendPoints` client-side to the corresponding number of trailing days; no additional API call is made
- [ ] AC-015: The chart renders a dual-series bar or line chart: one series for `passed` counts (green) and one for `failed` counts (red); each x-axis point is one calendar day
- [ ] AC-016: Days with zero runs are included in `trendPoints` with `passed: 0, failed: 0` so the x-axis is always continuous with no gaps
- [ ] AC-017: When every data point in the selected range has `passed: 0` and `failed: 0`, the chart renders an empty-data message ("No runs in this period") in place of the chart area
- [ ] AC-018: The selected time range is held in component state; it resets to **Last 30 days** on page reload

#### Edge Cases

- If a project was created fewer than 7 days ago the 7-day chart renders with fewer points — it does not error
- The `trendPoints` computation always uses UTC calendar days; the client displays dates in local timezone labels

---

### US-003: Run Detail — Structured Report View

**As a** QA lead  
**I want to** open any run from the history list and see a structured breakdown of its per-scenario results  
**So that** I can pinpoint which scenarios failed and understand the assertion diffs or step errors without opening the PM tool

#### Acceptance Criteria

- [ ] AC-019: Clicking a run row opens the run detail panel and fetches `GET /v1/stats/projects/{projectId}/runs/{runId}`
- [ ] AC-020: The run detail response includes: `storyTitle`, `verdict`, `recommendation`, `totalDurationMs`, `createdAt`, `scenarioResults: ScenarioSummary[]`, and `rawCommentMarkdown: string | null`
- [ ] AC-021: Each `ScenarioSummary` contains: `scenarioId` (string), `title` (string), `testType` (`"api"` or `"ui_e2e"`), `passed` (bool), `durationMs` (number), `errorSummary` (string or null), and `screenshotUris` (string array — always present, may be empty)
- [ ] AC-022: The structured view renders one scenario card per entry in `scenarioResults`; each card shows the scenario title, a pass/fail icon, duration in milliseconds, and — for failed scenarios — `errorSummary` in a monospace block
- [ ] AC-023: A loading skeleton matching the scenario card layout is shown while the run detail request is in progress
- [ ] AC-024: If the run detail request fails, an inline error message with a "Retry" action is shown in the panel
- [ ] AC-025: The `recommendation` value is rendered as a human-readable label: `"approve"` → `"Approve and merge"`, `"request_fixes"` → `"Request fixes"`, `"flag_for_manual_review"` → `"Flag for manual review"`
- [ ] AC-026: A "Raw report" toggle button is present in the panel header; it switches the panel body to the raw markdown view (US-004)
- [ ] AC-027: `TestResult` documents stored in Cosmos must include `scenarioResults: ScenarioSummary[]` and `rawCommentMarkdown: string`; these fields are populated by `ReportWriter` (feature 0030) — this feature extends the `TestResult` domain model to add them, requiring the ReportWriter implementer to populate them

#### Edge Cases

- If `rawCommentMarkdown` is `null` or empty, the "Raw report" toggle button is disabled and a tooltip reads "Raw report unavailable"
- A run with zero scenarios (e.g. `AgentRouter` returned `Skipped`) shows an empty scenario list with the message "No scenarios were generated for this run"
- Switching to a different run row while a panel is open closes the previous panel and opens the new one

---

### US-004: Run Detail — Raw Comment Toggle

**As a** QA lead  
**I want to** toggle from the structured scenario view to the raw markdown comment that was posted to the PM tool  
**So that** I can verify the exact content sent to ADO or Jira without leaving the portal

#### Acceptance Criteria

- [ ] AC-028: Activating the "Raw report" toggle replaces the scenario card list with the `rawCommentMarkdown` string rendered as formatted markdown
- [ ] AC-029: The toggle state is local to the currently open run detail panel; closing the panel and reopening a run resets to the structured view
- [ ] AC-030: The rendered markdown preserves code blocks, bold/italic text, and list formatting as produced by `ReportWriter`
- [ ] AC-031: Switching between runs while the raw view is active resets the toggle to the structured view for the newly selected run

#### Edge Cases

- Very long `rawCommentMarkdown` strings are displayed in a scrollable container within the panel rather than expanding the page

---

### US-005: Inline Screenshot Thumbnails

**As a** QA lead  
**I want to** see lazy-loaded screenshot thumbnails inline in failed UI E2E scenario cards  
**So that** I can visually inspect what the browser encountered without navigating to Azure Blob Storage manually

#### Acceptance Criteria

- [ ] AC-032: For `testType === "ui_e2e"` scenario cards, `screenshotUris` contains blob storage URLs captured during test execution (populated by feature 0029)
- [ ] AC-033: For failed UI E2E scenario cards, each URI in `screenshotUris` is rendered as a thumbnail image below the `errorSummary` block
- [ ] AC-034: Thumbnails are lazy-loaded using the browser's native `loading="lazy"` attribute
- [ ] AC-035: Clicking a thumbnail opens the full-resolution blob URL in a new browser tab
- [ ] AC-036: If `screenshotUris` is empty or null for a scenario, no thumbnail section is rendered for that card
- [ ] AC-037: Passed UI E2E scenario cards do not display screenshots regardless of whether `screenshotUris` contains values

#### Edge Cases

- A blob URL returning a non-2xx status renders a broken-image placeholder inline; no error is thrown and the rest of the panel continues to render

---

### US-006: Navigation — Dashboard and Settings Page Entry Points

**As a** QA lead  
**I want to** reach the project history page from both the dashboard card and the project settings page  
**So that** I have consistent access to run history from any point in the portal

#### Acceptance Criteria

- [ ] AC-038: Clicking a dashboard project card navigates to `/projects/:id/history` — this navigation is implemented by feature 0010 (AC-020); this feature only consumes the existing route
- [ ] AC-039: The project settings page includes a navigation control (link or tab) that takes the QA lead to `/projects/:id/history` using `PROJECT_HISTORY_ROUTE` from `routes.tsx`
- [ ] AC-040: The history page header includes a "Project Settings" button that navigates to `/projects/:id/settings` using `PROJECT_SETTINGS_ROUTE` from `routes.tsx`
- [ ] AC-041: No new route constants need to be introduced by this feature; both `PROJECT_HISTORY_ROUTE` and `PROJECT_SETTINGS_ROUTE` are already exported from `routes.tsx` (feature 0010)

#### Edge Cases

- If the project settings page navigation control to history is a tab item, the tab must not be marked "active" when the history page is the current route — routing is client-side and the tab styling is independent of the current URL on the settings page

---

### US-007: Data Isolation and Security

**As a** QA lead  
**I want to** be certain that history and run detail endpoints only return data belonging to my account  
**So that** no test result from another user is ever visible to me

#### Acceptance Criteria

- [ ] AC-042: `GET /v1/stats/projects/{projectId}/history` requires a valid Azure AD B2C JWT; a missing or invalid token returns `401 Unauthorized`
- [ ] AC-043: The endpoint validates that `projectId` belongs to the authenticated `userId`; if the project does not exist or belongs to a different user, it returns `404 Not Found` (not `403 Forbidden`)
- [ ] AC-044: `GET /v1/stats/projects/{projectId}/runs/{runId}` applies the same ownership validation — returns `404` if `runId` does not belong to a `TestResult` under `projectId` for the authenticated user
- [ ] AC-045: All Cosmos DB queries for this feature use `userId` as the partition key; no cross-partition queries are issued
- [ ] AC-046: `TestResult` records with `isDeleted: true` are excluded from all history and run detail responses

#### Edge Cases

- A request with a structurally valid but expired JWT receives `401 Unauthorized`
- A valid JWT for user A supplying a `projectId` owned by user B receives `404 Not Found`
