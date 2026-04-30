# User Stories — Automated API Test Execution (0003)

## Out of Scope (POC)

The following are deferred to MVP and explicitly excluded from this specification:

- Configurable request timeout per project — timeout is hardcoded for POC; covered by [0022]
- Multiple auth methods (API key, Basic Auth) — Bearer token only for POC; covered by [0023]
- Sequential step chaining — steps within a scenario run in parallel with no data passing between them
- Configurable product base URL — hardcoded per project for POC
- UI / E2E test execution — API testing only

---

## Stories

### US-001: Execute API Requests from Generated Scenarios

**As the** system  
**I want to** send the HTTP requests defined in the generated test scenarios to the product API  
**So that** each scenario is exercised against the real product without any manual effort from the QA lead

#### Acceptance Criteria

- [ ] AC-001: For each persisted scenario (from feature 0002), all steps are dispatched as HTTP requests in parallel.
- [ ] AC-002: Each request is constructed from the scenario step definition: method, URL path (appended to the hardcoded project base URL), headers, and request body.
- [ ] AC-003: All steps in a scenario are attempted regardless of whether other steps pass or fail.
- [ ] AC-004: Execution proceeds through all scenarios in the run regardless of individual scenario outcomes.
- [ ] AC-005: If a step definition is malformed (missing method or path), that step is marked as "Error — invalid request definition" and skipped without throwing an unhandled exception.

---

### US-002: Authenticate API Requests with Bearer Token

**As the** system  
**I want to** attach the project's configured Bearer token to every outgoing API request  
**So that** the product API accepts the requests without returning authentication errors

#### Acceptance Criteria

- [ ] AC-006: The Bearer token is read from the project's secure credentials store and injected as an `Authorization: Bearer <token>` header on every request.
- [ ] AC-007: If no Bearer token is configured for the project, requests are sent without an Authorization header.
- [ ] AC-008: The token value is never written to logs or included in error messages.

---

### US-003: Validate API Response Against Expected Schema

**As the** system  
**I want to** compare each API response against the expected status code and response body schema defined in the scenario step  
**So that** failures are detected automatically without manual inspection of responses

#### Acceptance Criteria

- [ ] AC-009: Each step result is evaluated against its expected HTTP status code — a mismatch marks the step as failed.
- [ ] AC-010: Each step result is evaluated against its expected response body schema — any missing required field or type mismatch marks the step as failed.
- [ ] AC-011: A step passes only when both the status code and body schema validations succeed.
- [ ] AC-012: The actual response (status code, headers, body) is captured and stored against the step result regardless of pass or fail outcome.

---

### US-004: Handle Request Timeout

**As the** system  
**I want to** enforce a hardcoded per-request timeout and handle requests that exceed it gracefully  
**So that** a slow or unresponsive product API cannot stall the entire test run indefinitely

#### Acceptance Criteria

- [ ] AC-013: Each HTTP request is subject to a hardcoded timeout of 10 seconds.
- [ ] AC-014: If a request does not complete within the timeout, the step is marked as "Failed — timeout" and execution continues with the remaining steps.
- [ ] AC-015: The timeout event is recorded in the step result with the elapsed duration.

---

### US-005: Record Execution Results per Step

**As the** system  
**I want to** persist the outcome of every executed step  
**So that** the test report (feature 0004) and run history (feature 0011) have complete, accurate data to display

#### Acceptance Criteria

- [ ] AC-016: For each executed step, the following are persisted: step title, status (Passed / Failed / Error / Timeout / Skipped), actual response (status code + body), expected schema, and duration in milliseconds.
- [ ] AC-017: Once all scenarios in a run are complete, the run status is set to "Passed" if all steps passed, or "Failed" if any step did not pass.
- [ ] AC-018: Run completion triggers the report delivery step (feature 0004) automatically.
