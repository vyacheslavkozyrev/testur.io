# User Stories — Story-Driven Test Scenario Generation (0002)

## Out of Scope (POC)

The following are deferred to MVP and explicitly excluded from this specification:

- Configurable testing strategy per project (predefined list: smoke, regression, BDD) — covered by [0006]
- Custom AI generation prompt per project — covered by [0008]
- QA lead review of generated scenarios before execution proceeds
- LLM retry logic on failure — run fails immediately
- Any LLM provider other than Anthropic Claude

---

## Stories

### US-001: Generate Test Scenarios from User Story

**As the** system  
**I want to** send the User Story's description and acceptance criteria to the Claude API and receive a structured set of test scenarios  
**So that** test coverage is derived automatically from the story without any manual test case authoring

#### Acceptance Criteria

- [ ] AC-001: When a test run is enqueued (via feature 0001), the system retrieves the full description and acceptance criteria of the triggering Jira User Story.
- [ ] AC-002: The content is sent to the Anthropic Claude API using a hardcoded system prompt that defines the POC testing strategy.
- [ ] AC-003: The system prompt instructs Claude to produce API test scenarios only (no UI steps).
- [ ] AC-004: Each generated scenario is a structured object containing: scenario title, ordered list of steps, and expected result per step.
- [ ] AC-005: The number of scenarios generated is determined by Claude based on story complexity — there is no fixed minimum or maximum.
- [ ] AC-006: If the Claude API returns an empty scenario list, the run is marked as failed and the QA lead is notified.

---

### US-002: Persist Generated Scenarios

**As the** system  
**I want to** store the generated test scenarios before passing them to the execution step  
**So that** scenarios are available for reporting and history even if execution subsequently fails

#### Acceptance Criteria

- [ ] AC-007: Generated scenarios are persisted to the database as part of the run record immediately after a successful Claude API response.
- [ ] AC-008: Each scenario record stores: run ID, scenario title, ordered steps, and expected results.
- [ ] AC-009: Once persisted, the execution step (feature 0003) is triggered automatically — no manual action or QA lead approval is required.
- [ ] AC-010: Persisted scenarios are visible in the run detail view in the project's run history.

---

### US-003: Handle Claude API Failure

**As a** QA lead  
**I want to** be notified immediately if scenario generation fails  
**So that** I am aware the run did not proceed and can investigate or re-trigger

#### Acceptance Criteria

- [ ] AC-011: If the Claude API call fails (network error, timeout, or non-2xx response), the run is marked as "Failed — scenario generation error" and no execution is attempted.
- [ ] AC-012: The QA lead receives a notification identifying the affected run and the nature of the failure.
- [ ] AC-013: The failed run is recorded in the project's run history with the error detail attached.
- [ ] AC-014: No retry is attempted — the QA lead must re-trigger by moving the story back to "In Testing" in Jira.
