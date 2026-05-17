# User Stories — Automated UI End-to-End Test Execution (0018)

## Out of Scope

The following are explicitly **not** part of this feature:

- Playwright browser engine implementation and screenshot capture — implemented by feature 0029 (`PlaywrightExecutor`, `BlobScreenshotStorage`, `IPlaywrightExecutor`)
- `UiE2eTestScenario` / `UiStep` domain models and generation — covered by feature 0028 (`UiE2eTestGeneratorAgent`)
- `UiE2eScenarioResult` / `StepExecutionResult` domain models — defined in feature 0029
- Screenshot attachment toggle and report template settings — covered by feature 0009
- AI verdict and report writing — covered by feature 0030 (`ReportWriter`)
- Non-Chromium browser targets (Firefox, WebKit) — post-MVP
- Parallel scenario execution within a single executor — scenarios run sequentially inside `PlaywrightExecutor` in MVP
- Post-MVP test types (smoke, a11y, visual, performance)
- Video recording of test runs — post-MVP
- Live streaming of step results to the portal during a run — post-MVP (feature 0043 adds SSE for status updates only)

---

## Stories

### US-001: View Per-Step Execution Detail for a UI E2E Scenario

**As a** QA lead  
**I want to** expand a UI E2E scenario in the run detail panel to see each step's result, error message, and screenshot  
**So that** I can pinpoint exactly which browser interaction failed and understand why without re-running the test

#### Acceptance Criteria

- [ ] AC-001: In the run detail panel, each UI E2E scenario card has an expand/collapse control (chevron or similar) that reveals the step-level breakdown.
- [ ] AC-002: The step breakdown shows one row per step in execution order, displaying: step index (1-based), action type (`navigate`, `click`, `fill`, `assert_visible`, `assert_text`, `assert_url`), and a pass/fail indicator.
- [ ] AC-003: For failed steps, the error message is displayed inline below the step row (e.g. `"Expected text 'Welcome' but got 'Error'"` or `"Skipped — preceding step failed"`).
- [ ] AC-004: For failed assertion steps that have a `screenshotBlobUri`, a screenshot thumbnail is displayed inline in the step row; clicking the thumbnail opens the full-size screenshot in a new browser tab.
- [ ] AC-005: Passed steps show no error message and no screenshot thumbnail.
- [ ] AC-006: Skipped steps (those with `errorMessage: "Skipped — preceding step failed"`) are visually distinguished from actively-failed steps (e.g. different icon or muted colour).
- [ ] AC-007: API scenario cards in the same run detail panel do not show a step expand control — the step breakdown is only shown for `testType: "ui_e2e"` scenarios.
- [ ] AC-008: The expand/collapse state is local to the current session and resets when the run detail panel is closed.
- [ ] AC-009: The component renders correctly when a UI E2E scenario has no failed steps (all passed) — the expand/collapse control is still present but no error messages or screenshots are shown.

---

### US-002: Surface UI E2E Step Results via the Stats API

**As the** portal frontend  
**I want** the `GET /v1/stats/projects/{projectId}/runs/{runId}` endpoint to include per-step execution detail for UI E2E scenarios  
**So that** the run detail panel can render the step breakdown without additional API calls

#### Acceptance Criteria

- [ ] AC-010: `ScenarioSummary` is extended with an optional `steps` property: `IReadOnlyList<StepSummary>?`. For `testType: "api"` scenarios the property is `null`; for `testType: "ui_e2e"` scenarios it is a non-null, non-empty list with one entry per executed step.
- [ ] AC-011: `StepSummary` is a new C# record in `Testurio.Core.Models` with properties: `int StepIndex`, `string Action`, `bool Passed`, `string? ErrorMessage`, `string? ScreenshotBlobUri`.
- [ ] AC-012: `ReportWriter` (feature 0030 pipeline stage) maps each `StepExecutionResult` to a `StepSummary` when building `ScenarioSummary` objects for `ui_e2e` result entries.
- [ ] AC-013: The `TestResult` Cosmos document stores the `steps` list as a nested array on each `ScenarioSummary` entry — no separate Cosmos container or document is required.
- [ ] AC-014: `RunDetailResponse` in `Testurio.Api` includes the `steps` property on each `ScenarioSummary` entry in the `scenarioResults` array.
- [ ] AC-015: Existing `TestResult` documents in Cosmos that pre-date this feature (with no `steps` field) are handled gracefully — `steps` deserialises to `null` and the frontend treats `null` as an empty step list.

---

### US-003: Display UI E2E Scenario Counts in the Run History Table

**As a** QA lead  
**I want** the run history table to show how many UI E2E scenarios passed out of the total  
**So that** I can assess test coverage at a glance without opening the run detail panel

#### Acceptance Criteria

- [ ] AC-016: The run history table row shows a UI E2E scenario count column, formatted as `{passedUiE2eScenarios}/{totalUiE2eScenarios}`.
- [ ] AC-017: When `totalUiE2eScenarios` is 0 for a run (e.g. the run only had API scenarios), the UI E2E column displays a dash (`—`) or an equivalent empty-state indicator.
- [ ] AC-018: The UI E2E scenario count column is present only when at least one run in the visible history contains `totalUiE2eScenarios > 0`; when no run in the current page has UI E2E scenarios, the column is hidden to reduce noise.
- [ ] AC-019: The `totalUiE2eScenarios` and `passedUiE2eScenarios` fields are already present on `RunHistoryItem` (defined in feature 0011) — no API change is required for this story; only a frontend rendering update is needed.

---

### US-004: Show Screenshot Thumbnails in the Run Detail Panel (Scenario Level)

**As a** QA lead  
**I want** to see screenshot thumbnails inline within a failed UI E2E scenario card in the run detail panel  
**So that** I can assess the visual state of the UI at the point of failure without opening a separate browser tab

#### Acceptance Criteria

- [ ] AC-020: A failed UI E2E scenario card that has one or more `screenshotUris` displays those thumbnails in a horizontal strip at the bottom of the collapsed card (i.e. without requiring the user to expand the step list).
- [ ] AC-021: Each thumbnail is a 120×80 px image rendered as a clickable link that opens the full-size screenshot in a new tab.
- [ ] AC-022: The thumbnail strip is hidden for passed UI E2E scenarios and for API scenarios regardless of pass/fail status.
- [ ] AC-023: The thumbnail strip is already implemented in the existing `ScenarioCard` component (feature 0011); this story confirms and validates that the behaviour is correct and covered by a component test — no new component code is required if the existing implementation is already correct.

---

### US-005: Validate Absence of UI E2E Steps on API Scenario Summaries

**As the** pipeline  
**I want** the `ReportWriter` to never attach step data to API scenario summaries  
**So that** the API response shape remains predictable and the frontend can use `testType` as the sole discriminator for rendering

#### Acceptance Criteria

- [ ] AC-024: When `ReportWriter` constructs a `ScenarioSummary` for an `ApiScenarioResult`, the `Steps` property is set to `null`.
- [ ] AC-025: The `GET /v1/stats/projects/{projectId}/runs/{runId}` response never returns a non-null `steps` array on a scenario entry with `testType: "api"`.
- [ ] AC-026: A unit test asserts that `ScenarioSummary` objects built from `ApiScenarioResult` inputs have `Steps == null`, and objects built from `UiE2eScenarioResult` inputs have `Steps` as a non-null list with the correct count.

---

### US-006: Step-Level Detail Accessible from Raw Report View

**As a** QA lead  
**I want** the raw Markdown report posted to the PM tool to include a step-by-step execution table for each UI E2E scenario  
**So that** the complete test evidence is available in the PM tool without requiring portal access

#### Acceptance Criteria

- [ ] AC-027: The `{{scenarios}}` placeholder in the report template expands to include, for each UI E2E scenario, a sub-section listing each step with its action type and pass/fail status.
- [ ] AC-028: Failed steps in the Markdown table include the error message on the same row or in a nested block.
- [ ] AC-029: For failed assertion steps with a `screenshotBlobUri`, the Markdown table includes an inline link to the screenshot (e.g. `[Screenshot](https://...)`).
- [ ] AC-030: API scenarios in the same report do not include a step sub-section — only assertion result rows (which are already rendered by feature 0030's `{{scenarios}}` token expansion).
- [ ] AC-031: If no UI E2E scenarios were executed in the run (e.g. `api`-only run), the step sub-section is absent from the report entirely.
