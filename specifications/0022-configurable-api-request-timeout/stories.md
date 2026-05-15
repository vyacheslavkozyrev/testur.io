# User Stories — Configurable API Request Timeout (0022)

## Out of Scope

The following are explicitly **not** part of this feature:

- Global (account-level) timeout override — timeout is configured per project only
- Different timeout values for API steps versus Playwright steps within the same project — one value applies to both executors
- Timeout configuration per scenario or per step — the setting is project-wide
- Retry-on-timeout behaviour — a timed-out step is marked failed and execution moves on; no automatic retry
- Real-time timeout monitoring or live progress indicators during a test run
- Alerting or notifications triggered specifically by timeout events — failures surface via the standard report
- Timeout for non-step operations (e.g. Key Vault lookups, Blob uploads, PM tool post-back) — only per-request step execution is governed by this setting

---

## Stories

### US-001: Set a Per-Request Timeout for a Project

**As a** QA lead
**I want to** configure a per-request timeout (in seconds) in my project's Testing Environment settings
**So that** slow or unresponsive product endpoints do not stall the entire test run indefinitely

#### Acceptance Criteria

- [ ] AC-001: The Testing Environment section of the project settings page contains a numeric "Request Timeout" field, rendered alongside the access configuration controls introduced in feature 0017
- [ ] AC-002: The field accepts integer values in the range 5–120 (inclusive); values outside this range show an inline validation error and the form is not submitted
- [ ] AC-003: The field is required; submitting with the field empty shows an inline validation error
- [ ] AC-004: Saving a valid value persists `requestTimeoutSeconds` on the Cosmos DB project document
- [ ] AC-005: The API returns `200 OK` with the updated project document on successful save
- [ ] AC-006: When a project is created (feature 0006), `requestTimeoutSeconds` is initialised to `30` if not explicitly supplied

---

### US-002: View the Current Timeout Setting

**As a** QA lead
**I want to** see the currently saved timeout value when I open the project settings page
**So that** I can verify or update the setting without guessing the current value

#### Acceptance Criteria

- [ ] AC-007: Opening the project settings page pre-fills the Request Timeout field with the value stored in the project document
- [ ] AC-008: `GET /api/projects/{projectId}` includes `requestTimeoutSeconds` in the response body
- [ ] AC-009: For projects created before this feature was introduced (i.e. where `requestTimeoutSeconds` is absent from the document), the API returns `30` as the effective value and the UI pre-fills `30`

---

### US-003: Enforce the Timeout on Each HTTP Executor Step

**As the** pipeline
**I want** `HttpExecutor` to apply the project's configured `requestTimeoutSeconds` to every individual HTTP request
**So that** a single slow endpoint cannot block the executor beyond the configured threshold

#### Acceptance Criteria

- [ ] AC-010: `HttpExecutor` reads `requestTimeoutSeconds` from the `ProjectConfig` passed to it at the start of execution and applies it as the per-request timeout via `HttpClient.Timeout` or an equivalent `CancellationTokenSource` linked to the run's `CancellationToken`
- [ ] AC-011: Each HTTP request is subject to its own independent timeout; the timeout counter resets for each new request in the scenario list
- [ ] AC-012: If a request does not complete within `requestTimeoutSeconds`, the step result is set to `Passed: false` with `ErrorMessage: "Timeout — request exceeded {n}s"` where `{n}` is the configured value; all assertions for that step are marked `Passed: false` with `Actual: "<timeout>"`
- [ ] AC-013: When a request times out, `HttpExecutor` continues to the next scenario in the list; the timeout of one scenario does not affect the execution of subsequent scenarios
- [ ] AC-014: The elapsed duration (in milliseconds) up to the moment of timeout is recorded in `ApiScenarioResult.DurationMs`

---

### US-004: Enforce the Timeout on Each Playwright Executor Step

**As the** pipeline
**I want** `PlaywrightExecutor` to apply the project's configured `requestTimeoutSeconds` to every individual Playwright step action
**So that** a hung browser action cannot block the executor beyond the configured threshold

#### Acceptance Criteria

- [ ] AC-015: `PlaywrightExecutor` reads `requestTimeoutSeconds` from the `ProjectConfig` passed to it and converts it to milliseconds for use as Playwright's per-action timeout (`Page.SetDefaultTimeout` or per-action `timeout` parameter)
- [ ] AC-016: Each Playwright step action is subject to its own independent timeout; the timeout counter resets for each new step action
- [ ] AC-017: If a Playwright step action does not complete within `requestTimeoutSeconds`, the step result is set to `Passed: false` with `ErrorMessage: "Timeout — action exceeded {n}s"` where `{n}` is the configured value; remaining steps in the same scenario are skipped (following the existing step-failure skip logic in feature 0029)
- [ ] AC-018: When a step times out in a scenario, `PlaywrightExecutor` continues to the next scenario in the list; the timeout of one scenario does not prevent execution of subsequent scenarios
- [ ] AC-019: The elapsed duration (in milliseconds) up to the moment of timeout is recorded in `UiE2eScenarioResult.DurationMs`

---

### US-005: Validate API-Layer Timeout Updates

**As a** QA lead
**I want to** receive meaningful error responses when I submit an invalid timeout value directly to the API
**So that** integration tooling and the portal both handle errors consistently

#### Acceptance Criteria

- [ ] AC-020: `PATCH /api/projects/{projectId}` (or the relevant project update endpoint) with `requestTimeoutSeconds` set to a non-integer or a value outside 5–120 returns `400 Bad Request` with a `ValidationProblemDetails` body identifying the field
- [ ] AC-021: A request targeting a project that belongs to a different user returns `403 Forbidden`
- [ ] AC-022: A request targeting a non-existent or soft-deleted project returns `404 Not Found`
