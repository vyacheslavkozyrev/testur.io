# User Stories — Configurable Work Item Type Filtering (0020)

## Out of Scope

The following are explicitly **not** part of this feature:

- PM tool connection setup (tokens, webhook URLs, auth method) — covered by feature 0007
- Status transition configuration after a test run (e.g. move to "Done" on pass) — covered by feature 0024
- Polling as a trigger method — covered by feature 0019
- Filtering by work item priority, label, or any field other than issue type
- Per-issue-type testing strategy overrides — all eligible types use the same project-level configuration
- Retroactive re-evaluation of already-queued or completed test runs when the filter is updated
- Wildcard or regex-based type matching — only exact string matching against the PM tool's issue type name

---

## Stories

### US-001: Configure Allowed Issue Types for a Project

**As a** QA lead
**I want to** specify which Jira or Azure DevOps issue types are eligible to trigger a test run
**So that** only meaningful work items (e.g. Stories, Bugs) cause tests to run and noise from tasks, sub-tasks, or epics is eliminated

#### Acceptance Criteria

- [ ] AC-001: The project settings page contains a "Work Item Type Filter" section within the Integrations area, visible only when a PM tool connection is configured
- [ ] AC-002: The section displays a multi-select input pre-populated with the default eligible types for the connected PM tool: `["Story", "Bug"]` for Jira; `["User Story", "Bug"]` for Azure DevOps
- [ ] AC-003: The QA lead can add or remove issue type strings from the list; each value is a free-text string matching the PM tool's exact issue type name (case-sensitive)
- [ ] AC-004: The QA lead cannot save the configuration with an empty list — at least one issue type must be selected; attempting to save with an empty list shows an inline validation error: "At least one work item type must be selected"
- [ ] AC-005: Saving a valid filter list calls `PATCH /api/projects/{projectId}` with a `allowedWorkItemTypes` field; the API returns `200 OK` with the updated project document
- [ ] AC-006: The `allowedWorkItemTypes` array is persisted in the project's Cosmos DB document alongside other project configuration fields
- [ ] AC-007: Changes apply only to future webhook events; test runs already enqueued at the time of the change are not affected

---

### US-002: View Current Work Item Type Filter Configuration

**As a** QA lead
**I want to** see the currently configured allowed work item types for my project at a glance
**So that** I can confirm the filter is set correctly before a test run fires

#### Acceptance Criteria

- [ ] AC-008: When a PM tool connection is configured, the "Work Item Type Filter" section displays the current `allowedWorkItemTypes` list
- [ ] AC-009: If no custom filter has been saved yet, the section shows the default list for the connected PM tool (`["Story", "Bug"]` for Jira; `["User Story", "Bug"]` for ADO)
- [ ] AC-010: The `GET /api/projects/{projectId}` response includes the `allowedWorkItemTypes` field (defaulting to the tool-appropriate list if not yet explicitly configured)
- [ ] AC-011: The "Work Item Type Filter" section is not displayed when no PM tool connection is configured for the project

---

### US-003: Filter Incoming Webhook Events by Issue Type

**As a** QA lead
**I want to** ensure that only webhook events for allowed issue types result in test runs
**So that** the pipeline does not waste runs on irrelevant work items

#### Acceptance Criteria

- [ ] AC-012: When a webhook event arrives, the API reads the `allowedWorkItemTypes` list from the project document before enqueuing the test run
- [ ] AC-013: If the incoming work item's issue type (as reported in the webhook payload) matches one of the allowed types (exact, case-sensitive string comparison), the event is processed normally and a test run is enqueued
- [ ] AC-014: If the incoming work item's issue type does not match any allowed type, the run is silently dropped — no test run is enqueued and no comment is posted to the originating ticket
- [ ] AC-015: A structured log entry is written for every dropped event: `{ eventType: "webhook_filtered", reason: "issue_type_not_allowed", issueType: "<actual type>", projectId: "<id>", timestamp: "<ISO 8601>" }`
- [ ] AC-016: If the project document has no `allowedWorkItemTypes` field set (legacy records), the handler falls back to the tool-appropriate default list and processes the event as if the default were explicitly configured

---

### US-004: Validate Issue Type Filter on the API

**As a** QA lead
**I want to** receive a clear error if I submit an invalid filter configuration via the API
**So that** the project configuration stays consistent and never enters an inoperable state

#### Acceptance Criteria

- [ ] AC-017: The `PATCH /api/projects/{projectId}` endpoint validates that `allowedWorkItemTypes` is a non-empty array of non-empty strings
- [ ] AC-018: Submitting an empty array `[]` returns `400 Bad Request` with a `ValidationProblemDetails` body: field `allowedWorkItemTypes`, message "At least one work item type must be selected"
- [ ] AC-019: Submitting an array that contains any empty string `""` returns `400 Bad Request` with a `ValidationProblemDetails` body: field `allowedWorkItemTypes`, message "Work item type values must be non-empty strings"
- [ ] AC-020: Submitting more than 20 issue type strings returns `400 Bad Request` with message "A maximum of 20 work item types may be configured"
- [ ] AC-021: A user can only update the filter on projects that belong to their own `userId`; attempting to patch another user's project returns `403 Forbidden`
