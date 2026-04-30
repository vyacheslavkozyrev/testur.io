# User Stories — Test Report Delivery to PM Tool (0004)

## Out of Scope (POC)

The following are deferred to MVP and explicitly excluded from this specification:

- Configurable report format and attachment settings — hardcoded for POC; covered by [0009]
- Automatic work item status transition after delivery — no status changes made; covered by [0024]
- Azure DevOps report delivery — Jira only
- Retry on delivery failure — single attempt only
- Attachment delivery (HTML/PDF) — Jira comment only

---

## Stories

### US-001: Post Run Report as a Jira Comment

**As the** system  
**I want to** post a structured report as a comment on the originating Jira work item once a run completes  
**So that** the QA lead and team can see test results without leaving Jira

#### Acceptance Criteria

- [ ] AC-001: When a run reaches a terminal state (Passed or Failed), a comment is posted to the Jira work item that triggered the run.
- [ ] AC-002: The comment is formatted in Jira markdown and includes: overall run status, run timestamp, total scenario count, pass count, and fail count.
- [ ] AC-003: The comment is posted using the Jira account credentials configured for the project.
- [ ] AC-004: If the work item no longer exists in Jira at delivery time, the error is logged and the run is marked as "Report delivery failed".

---

### US-002: Include Full Scenario and Step Detail in Report

**As a** QA lead  
**I want** the report comment to contain the full breakdown of every scenario and step, including request and response details  
**So that** I have enough information to diagnose failures without leaving Jira

#### Acceptance Criteria

- [ ] AC-005: The report comment lists every scenario by title, with its overall outcome (Passed / Failed).
- [ ] AC-006: For each scenario, every step is listed with: step title, status, expected status code, actual status code, expected response schema, actual response body, and duration in milliseconds.
- [ ] AC-007: Steps with status Timeout also include the elapsed duration at the point of timeout.
- [ ] AC-008: Steps with status Error include the error description (e.g. "invalid request definition").

---

### US-003: Apply Failure-Specific Detail for Failed Runs

**As a** QA lead  
**I want** failed run reports to surface failure information more prominently  
**So that** I can spot what went wrong at a glance without reading through all passing steps

#### Acceptance Criteria

- [ ] AC-009: In a failed run report, a dedicated "Failures" section appears at the top of the comment listing only the failed and errored steps across all scenarios.
- [ ] AC-010: Each entry in the Failures section includes: scenario title, step title, failure reason, and actual vs. expected values.
- [ ] AC-011: The full scenario-by-scenario breakdown (US-002) still appears below the Failures section in failed reports.
- [ ] AC-012: Passed run reports contain no Failures section.

---

### US-004: Handle Report Delivery Failure

**As a** QA lead  
**I want to** be notified if Testurio fails to post the report to Jira  
**So that** I know the evidence was not delivered and can take action

#### Acceptance Criteria

- [ ] AC-013: If the Jira API call to post the comment fails (network error, auth error, or non-2xx response), no retry is attempted.
- [ ] AC-014: The run is marked as "Report delivery failed" with the HTTP status and error message from Jira recorded against the run.
- [ ] AC-015: The QA lead receives a notification identifying the affected run and the delivery error.
- [ ] AC-016: The run's test results remain accessible in the Testurio run history regardless of delivery outcome.
