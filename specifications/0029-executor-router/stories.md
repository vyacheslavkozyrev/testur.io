# User Stories — Executor Router — HTTP & Playwright (0029)

## Out of Scope

The following are explicitly **not** part of this feature:

- Authentication injection into HTTP requests (Bearer token, API key, Basic Auth) — deferred to 0023
- Per-request configurable timeout — deferred to 0022; default `HttpClient` timeout applies
- Non-Chromium browser targets (Firefox, WebKit) — post-MVP
- Post-MVP test types (smoke, a11y, visual, performance) — only `api` and `ui_e2e` scenarios are executed here
- Parallel scenario execution within a single executor — scenarios run sequentially inside each executor in MVP
- Screenshot storage configuration (container name, path scheme, retention) — hardcoded in MVP
- Report composition and PM tool post-back — covered by 0030 (ReportWriter)
- `passRate` updates on reused memory scenarios — covered by 0031 (FeedbackLoop)

---

## Stories

### US-001: Route Scenarios to Executors in Parallel

**As the** pipeline  
**I want** the `ExecutorRouter` to dispatch `ApiScenarios` to `HttpExecutor` and `UiE2eScenarios` to `PlaywrightExecutor`, running both in parallel when both lists are non-empty  
**So that** execution time is bounded by the slower executor, not the sum of both

#### Acceptance Criteria

- [ ] AC-001: When both `GeneratorResults.ApiScenarios` and `GeneratorResults.UiE2eScenarios` are non-empty, `ExecutorRouter` invokes `HttpExecutor` and `PlaywrightExecutor` concurrently via `Task.WhenAll` and awaits both before returning.
- [ ] AC-002: When only `ApiScenarios` is non-empty, only `HttpExecutor` is invoked; `PlaywrightExecutor` is not started and `ExecutionResult.UiE2eResults` is an empty list.
- [ ] AC-003: When only `UiE2eScenarios` is non-empty, only `PlaywrightExecutor` is invoked; `HttpExecutor` is not started and `ExecutionResult.ApiResults` is an empty list.
- [ ] AC-004: When **both** `ApiScenarios` and `UiE2eScenarios` are empty, `ExecutorRouter` throws `ExecutorRouterException` with message `"No scenarios to execute — both API and UI E2E scenario lists are empty"` without invoking either executor.
- [ ] AC-005: The `CancellationToken` received by `ExecutorRouter` is forwarded to both executor calls.
- [ ] AC-006: `ExecutorRouter` returns an `ExecutionResult` that merges `ApiResults` from `HttpExecutor` and `UiE2eResults` from `PlaywrightExecutor` into a single record.

---

### US-002: Execute API Test Scenarios and Capture Assertion Diffs

**As the** pipeline  
**I want** `HttpExecutor` to send one HTTP request per `ApiTestScenario` and evaluate every assertion, recording the actual value alongside the expected value  
**So that** `ReportWriter` can show a precise diff for every failed assertion without re-executing the request

#### Acceptance Criteria

- [ ] AC-007: `HttpExecutor` sends one HTTP request per `ApiTestScenario` using the project's `productUrl` as the base URL; path and query string are appended directly from `ApiTestScenario.path`.
- [ ] AC-008: All scenario-level headers declared in `ApiTestScenario.headers` are added to the outgoing request.
- [ ] AC-009: For `POST`, `PUT`, and `PATCH` requests, `ApiTestScenario.body` (when non-null) is serialised to JSON and sent as the request body with `Content-Type: application/json`.
- [ ] AC-010: All assertions for a scenario are evaluated even if an earlier assertion fails; no assertion is skipped.
- [ ] AC-011: `status_code` assertion — `AssertionResult.Passed` is `true` when the actual HTTP status code equals `Assertion.expected`; `AssertionResult.Actual` is always populated with the string representation of the actual status code received.
- [ ] AC-012: `json_path` assertion — the JSONPath expression in `Assertion.path` is evaluated against the deserialized response body; `AssertionResult.Actual` is the string-serialised matched value, or `"<no match>"` when the path resolves to nothing. When `Assertion.expected` is `"*"`, the assertion passes if the path resolves to any non-null value.
- [ ] AC-013: `header` assertion — `AssertionResult.Actual` is the actual header value from the response, or `"<absent>"` when the header is not present; the assertion passes when `Actual` equals `Assertion.expected` (case-insensitive comparison).
- [ ] AC-014: A scenario's `Passed` field is `true` only when all `AssertionResult.Passed` values are `true`.
- [ ] AC-015: `ApiScenarioResult.DurationMs` captures the elapsed time in milliseconds from the moment the HTTP request is sent to the moment the full response body is received.
- [ ] AC-016: If the HTTP request itself fails (e.g. connection refused, DNS failure, `HttpRequestException`), all assertions for that scenario are marked `Passed: false` with `Actual` set to the exception message; the scenario's `Passed` is `false`.
- [ ] AC-017: `HttpExecutor` processes all scenarios in the list sequentially; a failure in one scenario does not prevent execution of subsequent scenarios.

---

### US-003: Execute UI E2E Test Scenarios in Headless Chromium

**As the** pipeline  
**I want** `PlaywrightExecutor` to drive a headless Chromium browser through each `UiE2eTestScenario`'s steps in order  
**So that** browser-based user flows are validated against the live product URL automatically

#### Acceptance Criteria

- [ ] AC-018: `PlaywrightExecutor` launches a single headless Chromium browser instance for the entire executor invocation; the browser is disposed after all scenarios complete, whether they pass or fail.
- [ ] AC-019: Each `UiE2eTestScenario` runs in its own isolated browser context (separate cookies, localStorage, and session state).
- [ ] AC-020: Steps are executed in the order they appear in `UiE2eTestScenario.steps`; if a step fails, the remaining steps in that scenario are skipped and each skipped step's `StepExecutionResult.Passed` is `false` with `ErrorMessage: "Skipped — preceding step failed"`.
- [ ] AC-021: `navigate` step — navigates to the given URL; fails if Playwright throws (e.g. net::ERR_NAME_NOT_RESOLVED).
- [ ] AC-022: `click` step — clicks the element matching the selector; fails if the element is not found or not clickable within Playwright's default timeout.
- [ ] AC-023: `fill` step — fills the input matching the selector with the given value; fails if the element is not found.
- [ ] AC-024: `assert_visible` step — asserts the element matching the selector is visible; fails if the element is absent or hidden.
- [ ] AC-025: `assert_text` step — asserts the text content of the element matching the selector equals `UiStep.expected` (exact, trimmed comparison); fails if the element is absent or text does not match.
- [ ] AC-026: `assert_url` step — asserts the current page URL equals `UiStep.expected` exactly, or starts with `UiStep.expected` when the expected value ends with `*`; fails if the condition is not met.
- [ ] AC-027: A scenario's `Passed` field is `true` only when every step completes without error.
- [ ] AC-028: `UiE2eScenarioResult.DurationMs` captures elapsed time in milliseconds from the first step start to the last step end (or failure) for the scenario.
- [ ] AC-029: `PlaywrightExecutor` processes all scenarios sequentially; a failure in one scenario does not prevent execution of subsequent scenarios.

---

### US-004: Capture Screenshots on Assertion Failure and Upload to Blob Storage

**As the** pipeline  
**I want** `PlaywrightExecutor` to capture a screenshot of the current browser state on any assertion-step failure and upload it to Azure Blob Storage  
**So that** `ReportWriter` can embed visual evidence of the failure in the PM tool comment

#### Acceptance Criteria

- [ ] AC-030: A screenshot is captured on failure of any assertion step (`assert_visible`, `assert_text`, `assert_url`); no screenshot is taken for non-assertion steps (`navigate`, `click`, `fill`) regardless of outcome.
- [ ] AC-031: Screenshots are uploaded to the `test-screenshots` container in Azure Blob Storage at path `{userId}/{runId}/{scenarioId}/step-{stepIndex}.png`.
- [ ] AC-032: On successful upload, `StepExecutionResult.ScreenshotBlobUri` is set to the full Blob Storage URI; for all other steps (passed or non-assertion), `ScreenshotBlobUri` is `null`.
- [ ] AC-033: If the Blob upload fails, a structured warning is logged (including `runId`, `scenarioId`, `stepIndex`, and the exception message) and `ScreenshotBlobUri` is set to `null`; the step's `Passed` status is **not** changed due to the upload failure.
- [ ] AC-034: Screenshots are PNG format.

---

### US-005: Surface Typed Execution Results to Stage 6

**As the** pipeline  
**I want** all execution outcomes to be collected into a single strongly-typed `ExecutionResult` record  
**So that** `ReportWriter` (stage 6) can produce a verdict without re-executing or re-interpreting any data

#### Acceptance Criteria

- [ ] AC-035: `ExecutionResult` is a C# record defined in `Testurio.Core` with properties `IReadOnlyList<ApiScenarioResult> ApiResults` and `IReadOnlyList<UiE2eScenarioResult> UiE2eResults`; both lists are never `null` (empty list when the corresponding executor was not invoked).
- [ ] AC-036: `ApiScenarioResult` is a C# record with: `string ScenarioId`, `string Title`, `bool Passed`, `long DurationMs`, `IReadOnlyList<AssertionResult> AssertionResults`.
- [ ] AC-037: `AssertionResult` is a C# record with: `string Type`, `bool Passed`, `string Expected`, `string Actual` (always populated — never `null`).
- [ ] AC-038: `UiE2eScenarioResult` is a C# record with: `string ScenarioId`, `string Title`, `bool Passed`, `long DurationMs`, `IReadOnlyList<StepExecutionResult> StepResults`.
- [ ] AC-039: `StepExecutionResult` is a C# record with: `int StepIndex`, `string Action`, `bool Passed`, `string? ErrorMessage` (null when passed), `string? ScreenshotBlobUri` (non-null only for failed assertion steps with a successful Blob upload).
- [ ] AC-040: All result records contain no execution logic — they are plain data containers.
- [ ] AC-041: After `Task.WhenAll` resolves, `TestRunJobProcessor` attaches the merged `ExecutionResult` to the run context and writes any accumulated executor-level errors to `TestRun.ExecutionWarnings` before invoking stage 6.
- [ ] AC-042: `TestRun.ExecutionWarnings` is an empty array when both executors complete without infrastructure-level errors — it is never `null`.
