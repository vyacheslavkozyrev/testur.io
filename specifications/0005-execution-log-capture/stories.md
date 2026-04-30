# User Stories — Execution Log Capture (0005)

## Out of Scope (POC)

The following are deferred to MVP and explicitly excluded from this specification:

- Toggle to include/exclude logs from the Jira report — always included for POC; covered by [0009]
- Masking of sensitive values (auth tokens, PII) in log content — verbatim capture only; QA lead's responsibility
- Log retention policy / scheduled purge — logs are retained for the lifetime of the run record

---

## Stories

### US-001: Capture Raw Log Entry per Executed Step

**As the** system  
**I want to** record a structured raw log entry for every executed step during a test run  
**So that** the complete evidence of what was sent and received is available for diagnosis independently of the structured pass/fail result

#### Acceptance Criteria

- [ ] AC-001: For each executed step, a log entry is created containing: scenario ID, step index, step title, timestamp (UTC), HTTP method, full request URL, all request headers, request body, HTTP response status code, all response headers, response body reference (see US-002), duration in milliseconds, and any execution error or timeout detail.
- [ ] AC-002: The log entry is created regardless of step outcome — Passed, Failed, Timeout, or Error.
- [ ] AC-003: The log entry is a separate record from the structured step result stored by feature 0003; both coexist and are linked by step ID.
- [ ] AC-004: If log entry persistence fails, the step result from feature 0003 is not affected — the failure is recorded as a system warning against the run.

---

### US-002: Store Large Response Bodies in Blob Storage

**As the** system  
**I want to** store response bodies that exceed a size threshold in blob storage rather than in the database  
**So that** large API responses (e.g. paginated lists) do not bloat the database while remaining fully accessible

#### Acceptance Criteria

- [ ] AC-005: Response bodies up to 10 KB are stored inline in the log entry record in the database.
- [ ] AC-006: Response bodies exceeding 10 KB are uploaded to blob storage and the log entry stores a reference URL pointing to the blob.
- [ ] AC-007: The reference URL is resolved transparently when the log is retrieved — callers receive the response body content regardless of where it is stored.
- [ ] AC-008: If the blob upload fails, the response body is truncated to 10 KB, stored inline, and the log entry is flagged as "response truncated — blob upload failed".

---

### US-003: Retain Logs for the Lifetime of the Run Record

**As a** QA lead  
**I want** execution logs to remain accessible for as long as the associated run record exists  
**So that** I can revisit past run evidence when investigating recurring failures

#### Acceptance Criteria

- [ ] AC-009: Log entries and their associated blobs are retained in storage for as long as the parent run record exists.
- [ ] AC-010: When a run record is deleted, all associated log entries and blobs are deleted in the same operation.
- [ ] AC-011: Log entries are retrievable by run ID and by step ID from the Testurio run history view.

---

### US-004: Include Execution Logs in the Jira Report Comment

**As a** QA lead  
**I want** the full step-by-step execution log to be included in the Jira report comment  
**So that** all request and response evidence is visible directly in the work item without opening Testurio

#### Acceptance Criteria

- [ ] AC-012: The Jira report comment produced by feature 0004 includes the execution log for every step, appended below the scenario breakdown.
- [ ] AC-013: Each log block in the comment shows: request (method, URL, headers, body) and response (status, headers, body or truncation notice) formatted as Jira markdown code blocks.
- [ ] AC-014: If a response body was stored in blob storage, the comment includes the reference URL rather than inlining the full content.
- [ ] AC-015: Log inclusion applies to all runs — passed and failed — with no toggle at POC stage.
