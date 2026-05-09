# User Stories — Project Creation & Core Configuration (0006)

## Out of Scope

The following are explicitly **not** part of this feature:

- PM tool integration (ADO / Jira tokens and webhook setup) — covered by feature 0007
- Custom test generation prompt — covered by feature 0008
- Report format and attachment settings — covered by feature 0009
- Testing environment access configuration (IP allowlist, Basic Auth, header token) — covered by feature 0017
- Project name uniqueness enforcement — names are free-text with no uniqueness constraint
- Draft / partial-save mode — all fields are required; no incomplete project documents are persisted
- Per-user project cap — no limit on the number of projects for MVP
- Live URL reachability check at creation time — format validation only
- Structured testing strategy options — the strategy field is free-text in MVP
- Team / multi-user project sharing — single-user accounts only in v1

---

## Stories

### US-001: Create a New Project

**As a** QA lead
**I want to** create a new project by providing a name, product URL, and testing strategy
**So that** I can model my product inside Testurio and begin configuring automated testing

#### Acceptance Criteria

- [ ] AC-001: A "Create Project" action is accessible from the Dashboard empty state (feature 0010 entry point)
- [ ] AC-002: The creation form contains exactly three required fields: Name (free-text), Product URL (URL format), and Testing Strategy (free-text)
- [ ] AC-003: Submitting the form with all three fields populated creates a project document in Cosmos DB under the authenticated user's `userId` partition
- [ ] AC-004: On successful creation the user is navigated to the newly created project's settings or detail page
- [ ] AC-005: The API returns `201 Created` with the new project document in the response body
- [ ] AC-006: A Key Vault namespace is provisioned for the project at creation time (e.g. `projects/{projectId}/`) to hold future secrets
- [ ] AC-007: The created project record includes: `projectId` (system-generated GUID), `userId`, `name`, `productUrl`, `testingStrategy`, `isDeleted: false`, `createdAt`, `updatedAt`

---

### US-002: Validate Project Fields on Creation

**As a** QA lead
**I want to** receive clear validation feedback when I submit incomplete or invalid project data
**So that** I understand exactly what needs to be corrected before the project is saved

#### Acceptance Criteria

- [ ] AC-008: Submitting with any required field empty shows an inline validation error on that field; the form is not submitted to the API
- [ ] AC-009: Submitting a `productUrl` that is not a valid URL format (e.g. missing scheme, malformed host) shows an inline validation error on the URL field
- [ ] AC-010: The API independently validates all fields and returns `400 Bad Request` with a `ValidationProblemDetails` body listing each invalid field if invalid data is submitted directly
- [ ] AC-011: Name field accepts any non-empty string up to 200 characters; exceeding 200 characters triggers a validation error
- [ ] AC-012: Testing Strategy field accepts any non-empty string up to 500 characters; exceeding 500 characters triggers a validation error

---

### US-003: Edit an Existing Project's Core Configuration

**As a** QA lead
**I want to** update a project's name, product URL, or testing strategy after it has been created
**So that** I can keep the project configuration accurate as my product evolves

#### Acceptance Criteria

- [ ] AC-013: An "Edit" action is accessible from the project's settings or detail page
- [ ] AC-014: The edit form is pre-populated with the project's current Name, Product URL, and Testing Strategy values
- [ ] AC-015: Saving valid changes updates the Cosmos DB document and refreshes `updatedAt`
- [ ] AC-016: The API returns `200 OK` with the updated project document in the response body
- [ ] AC-017: Saving with any field empty or invalid shows the same validation errors described in US-002
- [ ] AC-018: A user can only edit projects that belong to their own `userId`; attempting to edit another user's project returns `403 Forbidden`

---

### US-004: List All Projects

**As a** QA lead
**I want to** retrieve all my projects
**So that** the Dashboard and other views can display project summaries

#### Acceptance Criteria

- [ ] AC-019: The `GET /api/projects` endpoint returns only projects belonging to the authenticated user's `userId`
- [ ] AC-020: Soft-deleted projects (`isDeleted: true`) are excluded from the list response
- [ ] AC-021: The response is an array of project summary objects; each item includes at minimum: `projectId`, `name`, `productUrl`, `testingStrategy`, `createdAt`, `updatedAt`
- [ ] AC-022: An authenticated user with no projects receives an empty array `[]`, not an error

---

### US-005: View a Single Project

**As a** QA lead
**I want to** retrieve the full details of one of my projects
**So that** the project settings page can display and allow editing of the current configuration

#### Acceptance Criteria

- [ ] AC-023: `GET /api/projects/{projectId}` returns the full project document for a project belonging to the authenticated user
- [ ] AC-024: Requesting a project that belongs to a different user returns `403 Forbidden`
- [ ] AC-025: Requesting a project `projectId` that does not exist returns `404 Not Found`
- [ ] AC-026: Requesting a soft-deleted project returns `404 Not Found`

---

### US-006: Soft Delete a Project

**As a** QA lead
**I want to** delete a project I no longer need
**So that** it no longer appears in my project list or triggers new test runs

#### Acceptance Criteria

- [ ] AC-027: A "Delete Project" action is accessible from the project's settings or detail page, behind a confirmation dialog
- [ ] AC-028: Confirming deletion calls `DELETE /api/projects/{projectId}`, which sets `isDeleted: true` and updates `updatedAt` on the Cosmos document; the document is not physically removed
- [ ] AC-029: The API returns `204 No Content` on successful soft delete
- [ ] AC-030: After deletion the user is navigated back to the Dashboard
- [ ] AC-031: A user can only delete projects that belong to their own `userId`; attempting to delete another user's project returns `403 Forbidden`
- [ ] AC-032: Deleting an already-deleted project returns `404 Not Found`
- [ ] AC-033: The soft-deleted project no longer appears in the project list (US-004) or resolves via direct lookup (US-005)
