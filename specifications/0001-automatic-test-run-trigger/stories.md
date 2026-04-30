# User Stories — Automatic Test Run Trigger (0001)

## Out of Scope (POC)

The following are deferred to MVP and explicitly excluded from this specification:

- Polling-based trigger — webhook is the only supported notification method
- Azure DevOps integration — Jira only
- Configurable work item types — hardcoded to User Story
- Work item enrichment flow — incomplete items are skipped, not held for editing
- Plan-tier daily trigger rate limits
- UI / E2E test triggering — API testing only
- Self-hosted LLM — commercial LLM API only

---

## Stories

### US-001: Receive Webhook Trigger from Jira

**As a** QA lead  
**I want** Testurio to start an API test run when Jira pushes a status-change event for a User Story  
**So that** testing begins automatically without any manual action on my part

#### Acceptance Criteria

- [ ] AC-001: Testurio exposes a per-project webhook endpoint that accepts Jira `issue_transitioned` events.
- [ ] AC-002: A test run is enqueued only when the transitioning issue is of type User Story and the new status matches the project's configured "In Testing" status label.
- [ ] AC-003: Webhook payloads with an invalid or missing Jira secret token are rejected with `401` and not enqueued.
- [ ] AC-004: Valid events that do not match the trigger condition (wrong issue type or status) are acknowledged with `200` and silently ignored.

---

### US-002: Queue Triggers During an Active Run

**As a** QA lead  
**I want** stories that enter "In Testing" while a run is already in progress to be queued automatically  
**So that** no trigger is lost and runs execute in the order they were received

#### Acceptance Criteria

- [ ] AC-005: When a trigger arrives for a project with an active run, the new item is added to the project's run queue.
- [ ] AC-006: Queued items are processed sequentially in FIFO order once the active run completes.
- [ ] AC-007: If a story is already in the queue and the same webhook fires again for it, a duplicate entry is not created.

---

### US-003: Skip and Notify on Missing Description or Acceptance Criteria

**As a** QA lead  
**I want** to be notified when a triggering User Story has no description or acceptance criteria  
**So that** I am aware the run was skipped and can fix the story before re-triggering

#### Acceptance Criteria

- [ ] AC-008: When a trigger is received for a User Story with an empty description **or** no acceptance criteria, the item is not enqueued.
- [ ] AC-009: Testurio posts a comment on the Jira issue identifying what is missing (description, acceptance criteria, or both).
- [ ] AC-010: The skipped item is recorded in the project's run history with status "Skipped — incomplete story".
- [ ] AC-011: Once the QA lead updates the story and it re-enters "In Testing", the run is enqueued normally.
