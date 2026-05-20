# User Stories — Automatic Work Item Status Transition After Report Delivery (0024)

## Out of Scope

The following are explicitly **not** part of this feature:

- Status transition on run states other than Passed or Failed (e.g. Skipped, ReportDeliveryFailed) — no transition is made in those cases.
- Custom transition logic or multi-step transition workflows — a single direct transition per run outcome only.
- Notification to the QA lead when a transition fails — failure is logged to Application Insights and recorded on the run; no separate alert is raised (consistent with the delivery-failure pattern in feature 0004).
- Bulk or retroactive transitions for historical runs — only the run that just completed triggers the transition.
- Validation that the configured target status actually exists in the PM tool at save time — validation happens at configuration save; if the status is later deleted in the PM tool the transition will fail gracefully at runtime.
- Any frontend indication of which status a work item was transitioned to on the run history/report page — the portal shows transition outcome (succeeded / failed / not configured) but does not re-display the PM tool's current status.

---

## Stories

### US-001: Configure Post-Run Status Transition for Jira

**As a** QA lead  
**I want to** configure which Jira status the work item should transition to after a passed run and after a failed run  
**So that** Testurio closes the testing loop automatically without me touching Jira after every run

#### Acceptance Criteria

- [ ] AC-001: The Integration settings tab exposes two optional text fields: "Transition to status on pass" and "Transition to status on fail", visible only when the project's PM tool is Jira and the integration status is `active`.
- [ ] AC-002: Both fields accept a free-text Jira status name (e.g. "Done", "Rejected"). Each field is independent — either or both may be left blank.
- [ ] AC-003: When both fields are blank, no transition is made after a run (default behaviour, identical to the POC).
- [ ] AC-004: Saving the Jira connection persists the configured status names to the project document. Saving with blank values clears any previously stored values.
- [ ] AC-005: The configured status names are returned in the `GET /v1/projects/{id}/integrations` response so the portal can pre-populate the fields on reload.

---

### US-002: Configure Post-Run Status Transition for Azure DevOps

**As a** QA lead  
**I want to** configure which Azure DevOps state the work item should transition to after a passed run and after a failed run  
**So that** my ADO board reflects the test outcome without manual intervention

#### Acceptance Criteria

- [ ] AC-006: The Integration settings tab exposes two optional text fields: "Transition to state on pass" and "Transition to state on fail", visible only when the project's PM tool is ADO and the integration status is `active`.
- [ ] AC-007: Both fields accept a free-text ADO state name (e.g. "Closed", "Active"). Each field is independent — either or both may be left blank.
- [ ] AC-008: When both fields are blank, no transition is made after a run.
- [ ] AC-009: Saving the ADO connection persists the configured state names to the project document. Saving with blank values clears any previously stored values.
- [ ] AC-010: The configured state names are returned in the `GET /v1/projects/{id}/integrations` response so the portal can pre-populate the fields on reload.

---

### US-003: Automatic Status Transition After a Passed Run

**As the** system  
**I want to** transition the originating Jira or ADO work item to the configured "on pass" status immediately after report delivery succeeds  
**So that** the board reflects a successful test outcome automatically

#### Acceptance Criteria

- [ ] AC-011: After stage 6 (ReportWriter) completes for a run that resulted in all scenarios passing, the pipeline attempts a status transition if the project has a non-blank `PassedTransitionStatus` configured for the active PM tool.
- [ ] AC-012: If `PassedTransitionStatus` is blank or null, no transition attempt is made and the pipeline continues normally.
- [ ] AC-013: The transition is performed by calling the appropriate PM tool API (Jira transition endpoint or ADO PATCH state endpoint) using the credentials already stored for the project.
- [ ] AC-014: On a successful transition, the `TestRun` record is updated with `StatusTransitionOutcome = Succeeded` and the name of the status transitioned to.
- [ ] AC-015: A successful transition is logged to Application Insights at Information level.

---

### US-004: Automatic Status Transition After a Failed Run

**As the** system  
**I want to** transition the originating Jira or ADO work item to the configured "on fail" status immediately after report delivery succeeds  
**So that** the board flags the work item for rework without waiting for a human

#### Acceptance Criteria

- [ ] AC-016: After stage 6 (ReportWriter) completes for a run that resulted in at least one failing scenario, the pipeline attempts a status transition if the project has a non-blank `FailedTransitionStatus` configured for the active PM tool.
- [ ] AC-017: If `FailedTransitionStatus` is blank or null, no transition attempt is made and the pipeline continues normally.
- [ ] AC-018: The transition uses the same PM tool API call and credentials as AC-013.
- [ ] AC-019: On a successful transition, the `TestRun` record is updated with `StatusTransitionOutcome = Succeeded`.
- [ ] AC-020: A successful transition is logged to Application Insights at Information level.

---

### US-005: Graceful Handling of Transition Failure

**As a** QA lead  
**I want** a transition failure to be recorded against the run without blocking the pipeline  
**So that** a broken PM tool state does not prevent the test evidence from being preserved in the Testurio run history

#### Acceptance Criteria

- [ ] AC-021: If the PM tool API call for the transition fails (network error, auth error, non-2xx response, or status name not found in the PM tool), the error is caught and not re-thrown.
- [ ] AC-022: The `TestRun` record is updated with `StatusTransitionOutcome = Failed` and a `StatusTransitionError` field containing the HTTP status code and error body (or network error message).
- [ ] AC-023: The failure is logged to Application Insights at Warning level, including the run ID, project ID, target status name, and error detail.
- [ ] AC-024: The pipeline continues normally after a transition failure — the run status (`Completed` or `Failed`) and the Cosmos `TestResult` record are unaffected.
- [ ] AC-025: The transition outcome (`Succeeded`, `Failed`, or `NotConfigured`) is visible in the run detail panel in the portal, with the error detail shown when the outcome is `Failed`.

---

### US-006: View Transition Outcome in Run History

**As a** QA lead  
**I want to** see whether the post-run status transition succeeded or failed in the run detail panel  
**So that** I know whether the PM tool board was updated automatically and can take action if it was not

#### Acceptance Criteria

- [ ] AC-026: The `GET /v1/projects/{id}/runs/{runId}` response includes `statusTransitionOutcome` (`succeeded`, `failed`, or `notConfigured`) and `statusTransitionError` (null on success or when not configured; error string on failure).
- [ ] AC-027: The run detail panel in the portal displays a "Status transition" row showing one of: "Not configured", "Succeeded — moved to [status name]", or "Failed — [error detail]".
- [ ] AC-028: When `statusTransitionOutcome` is `failed`, the error detail is shown in a visually distinct style (e.g. amber/warning colour) to draw the QA lead's attention.
