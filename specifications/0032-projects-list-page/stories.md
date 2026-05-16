# User Stories — Projects List Page (0032)

## Out of Scope

The following are explicitly **not** part of this feature:

- The Dashboard page at `/dashboard` with run-status badges and quota bar — covered by feature 0010
- Project creation form content — covered by feature 0006; this feature only triggers navigation to `/projects/new`
- Project settings page content — covered by feature 0006; this feature only triggers navigation to `/projects/:id/settings`
- Per-project test history page content — covered by feature 0011
- Deleting a project — covered by feature 0006; the delete action lives on the settings page
- Filtering or searching of project cards
- Pagination — all active projects for the authenticated user are returned in a single response for MVP
- Run-status badges or quota usage display — those belong on the Dashboard (feature 0010)
- Real-time updates to the project list via SSE
- Team / multi-user project sharing — single-user accounts only in v1

---

## Stories

### US-001: Projects List Card Grid

**As a** QA lead
**I want to** see all my projects displayed as cards sorted by most recently created, with the project name, URL, and testing strategy visible on each card
**So that** I can quickly identify and navigate to any project without opening the Dashboard

#### Acceptance Criteria

- [ ] AC-001: The projects list page is accessible at `/projects` via the "Projects" sidebar link
- [ ] AC-002: Each project card displays: project name, product URL, and testing strategy description; `customPrompt` is not shown on the card
- [ ] AC-003: Cards are sorted client-side by `createdAt` descending — most recently created project first
- [ ] AC-004: The testing strategy description is truncated client-side to approximately 120 characters with a trailing ellipsis (`…`) when the full text exceeds that length; the full text is fetched from the API and truncation is applied in the UI
- [ ] AC-005: A loading skeleton matching the card grid layout is shown while project data is being fetched
- [ ] AC-006: If the API request fails, an inline error state is shown with a "Retry" action that re-triggers the fetch
- [ ] AC-007: A "Create Project" button is always visible in the page header regardless of how many projects exist; clicking it navigates to `/projects/new`
- [ ] AC-008: The projects list fetches data from `GET /v1/projects`; no secondary request is made per card

#### Edge Cases

- A project with a testing strategy of exactly 120 characters or fewer is shown in full without an ellipsis
- A project with a very long name does not break the card layout; the name wraps or truncates with ellipsis per MUI `Typography` `noWrap` or `overflow` styling

---

### US-002: Empty State — No Projects CTA

**As a** QA lead who has just created an account
**I want to** see a clear call to action when I have no projects yet
**So that** I know how to get started without being confused by a blank screen

#### Acceptance Criteria

- [ ] AC-009: When `GET /v1/projects` returns an empty array, the card grid is replaced by a centred empty state panel containing: an illustrative icon, a heading, a supporting description, and a "Create your first project" button
- [ ] AC-010: The "Create your first project" button navigates to `/projects/new`
- [ ] AC-011: The empty state panel is not shown while a fetch is in progress or in an error state — only when a successful response confirms zero projects
- [ ] AC-012: The "Create Project" button in the page header (AC-007) remains visible in the empty state

#### Edge Cases

- A user who deletes their last project sees the empty state on their next visit to `/projects`
- The empty state must not flash briefly before the loading skeleton disappears — show skeleton first, then empty state on confirmed empty response

---

### US-003: Navigate to Project History

**As a** QA lead
**I want to** click a project card to open that project's test history
**So that** I can investigate individual run results directly from the projects list

#### Acceptance Criteria

- [ ] AC-013: Clicking anywhere on a project card (outside the edit icon button) navigates to the per-project history page at `/projects/:id/history` where `:id` is the `projectId`
- [ ] AC-014: The card click target covers the entire card surface; the edit icon button (AC-016) is an independent action that does not propagate the click to the card navigation handler
- [ ] AC-015: Card navigation uses client-side routing (Next.js `Link` or `router.push`) — not a full page reload

#### Edge Cases

- If a project is deleted between the list load and the card click, the history page at `/projects/:id/history` handles the 404 from its own API call (feature 0011's responsibility); this feature only performs the navigation

---

### US-004: Edit Project from Card

**As a** QA lead
**I want to** click an edit icon on a project card to open that project's settings
**So that** I can modify the project configuration without first navigating to its history page

#### Acceptance Criteria

- [ ] AC-016: Each project card includes an edit icon button (MUI `IconButton` with `EditOutlined` icon) positioned in the top-right corner of the card
- [ ] AC-017: Clicking the edit icon button navigates to `/projects/:id/settings` where `:id` is the `projectId`
- [ ] AC-018: The edit icon button click does not propagate to the card-level navigation handler; only the settings page opens, not the history page
- [ ] AC-019: The edit icon button has `aria-label="Edit project"` for screen reader accessibility
- [ ] AC-020: Navigation to the settings page uses client-side routing — not a full page reload

#### Edge Cases

- If a project is deleted between the list load and the edit icon click, the settings page at `/projects/:id/settings` handles the 404 from its own API call (feature 0006's responsibility); this feature only performs the navigation

---

### US-005: Projects List Security and Data Isolation

**As a** QA lead
**I want to** be confident that the projects list only shows projects belonging to my account
**So that** no information from other users is ever visible to me

#### Acceptance Criteria

- [ ] AC-021: The projects list page is only accessible to authenticated users; unauthenticated access redirects to sign-in (enforced by the auth guard from feature 0010a)
- [ ] AC-022: `GET /v1/projects` requires a valid Azure AD B2C JWT; a missing or invalid token returns `401 Unauthorized`
- [ ] AC-023: The `userId` extracted from the JWT is used as the Cosmos DB partition key; no query is issued without this scope
- [ ] AC-024: Soft-deleted projects (`isDeleted: true`) are excluded from the API response
- [ ] AC-025: The endpoint returns `200 OK` with an empty array when the authenticated user has no active projects

#### Edge Cases

- A request with a structurally valid but expired JWT receives `401 Unauthorized`
- A request with a valid JWT that includes a caller-supplied `userId` query parameter ignores that parameter; the server always derives `userId` from the token
