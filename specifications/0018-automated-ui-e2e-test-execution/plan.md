# Implementation Plan — Automated UI End-to-End Test Execution (0018)

## Tasks

- [x] T001 [Domain] Add `StepSummary` record (`int StepIndex`, `string Action`, `bool Passed`, `string? ErrorMessage`, `string? ScreenshotBlobUri`) to `Testurio.Core.Models` — `source/Testurio.Core/Models/ReportContent.cs`
- [x] T002 [Domain] Extend `ScenarioSummary` record with `IReadOnlyList<StepSummary>? Steps` property (nullable; `null` for `api` scenarios, non-null list for `ui_e2e` scenarios) — `source/Testurio.Core/Models/ReportContent.cs`
- [x] T003 [App] Update `ReportWriter.BuildScenarioSummaries` to map each `UiE2eScenarioResult.StepResults` entry to a `StepSummary` and set `Steps` on the resulting `ScenarioSummary`; set `Steps = null` for API scenario summaries — `source/Testurio.Pipeline.ReportWriter/ReportWriter.cs`
- [x] T004 [App] Update `PmCommentFormatter.Format` to render a per-step Markdown sub-table for each UI E2E scenario inside the `{{scenarios}}` block: one row per step showing action, pass/fail, and error message; include `[Screenshot](uri)` link on failed assertion steps; omit the sub-table entirely for API scenarios and when no UI E2E scenarios were executed — `source/Testurio.Pipeline.ReportWriter/PmCommentFormatter.cs`
- [ ] T005 [Infra] Ensure `TestResultRepository` round-trips the `steps` nested array when writing/reading `ScenarioSummary` entries in Cosmos — `source/Testurio.Infrastructure/Cosmos/TestResultRepository.cs`
- [ ] T006 [UI] Extend `ScenarioSummary` TypeScript type with `steps: StepSummary[] | null` and add `StepSummary` interface (`stepIndex: number`, `action: string`, `passed: boolean`, `errorMessage: string | null`, `screenshotBlobUri: string | null`) — `source/Testurio.Web/src/types/history.types.ts`
- [ ] T007 [UI] Update MSW mock handler for `GET /v1/stats/projects/:projectId/runs/:runId` to return `steps` arrays on `ui_e2e` scenario entries and `null` on `api` entries — `source/Testurio.Web/src/mocks/handlers/history.ts`
- [ ] T008 [UI] Create `UiStepList` component — an expand/collapse list showing one row per step with: 1-based index, action label, pass/fail icon; failed steps show error message inline; failed assertion steps with `screenshotBlobUri` show a 120×80 thumbnail linking to the screenshot in a new tab; skipped steps are visually muted — `source/Testurio.Web/src/components/UiStepList/UiStepList.tsx`
- [ ] T009 [UI] Update `ScenarioCard` to render a chevron expand/collapse control for `testType === "ui_e2e"` scenarios; when expanded, render `UiStepList` with the scenario's `steps`; API scenario cards render no expand control; expand state is component-local (`useState`) and resets when the parent panel unmounts — `source/Testurio.Web/src/components/ScenarioCard/ScenarioCard.tsx`
- [ ] T010 [UI] Update `RunHistoryTable` to add a `UI E2E` column showing `{passedUiE2eScenarios}/{totalUiE2eScenarios}` for runs where `totalUiE2eScenarios > 0` and a dash for runs with no UI E2E scenarios; the column is present whenever at least one run in the rendered list has `totalUiE2eScenarios > 0` — `source/Testurio.Web/src/components/RunHistoryTable/RunHistoryTable.tsx`
- [ ] T011 [UI] Add translation keys for `UiStepList` and the new `RunHistoryTable` UI E2E column to the `history` locale namespace — `source/Testurio.Web/src/locales/en/history.json`
- [ ] T012 [Test] Unit tests for `PmCommentFormatter` additions: PASSED UI E2E scenario — no step sub-table; FAILED UI E2E with failed assertion step — sub-table row includes error message and screenshot link; FAILED UI E2E with skipped steps — skipped rows rendered; API scenario in same run — no step sub-table; `null` steps field — no sub-table rendered — `tests/Testurio.UnitTests/Pipeline/ReportWriter/PmCommentFormatterTests.cs`
- [ ] T013 [Test] Unit tests for `ReportWriter.BuildScenarioSummaries` additions: API result → `Steps == null`; UI E2E result → `Steps` is non-null list with correct `StepIndex`, `Action`, `Passed`, `ErrorMessage`, `ScreenshotBlobUri`; mixed API + UI E2E run → each summary has the correct nullability — `tests/Testurio.UnitTests/Pipeline/ReportWriter/ReportWriterTests.cs`
- [ ] T014 [Test] Frontend component tests for `UiStepList`: renders each step row with action label and pass/fail icon; failed step shows error message; assertion step with blob URI shows thumbnail and anchor with `target="_blank"`; passed step shows no error and no screenshot; skipped step (`errorMessage` starts with `"Skipped"`) renders with muted style — `source/Testurio.Web/src/components/UiStepList/UiStepList.test.tsx`
- [ ] T015 [Test] Frontend component tests for updated `ScenarioCard`: `api` scenario — no chevron, no `UiStepList`; `ui_e2e` scenario — chevron present, `UiStepList` hidden initially, visible after click; re-closing collapses the list; existing screenshot thumbnail tests still pass — `source/Testurio.Web/src/components/ScenarioCard/ScenarioCard.test.tsx`
- [ ] T016 [Test] Frontend component tests for updated `RunHistoryTable`: all runs have `totalUiE2eScenarios === 0` → no UI E2E column; at least one run has `totalUiE2eScenarios > 0` → column present; run with `totalUiE2eScenarios === 0` in mixed list → dash rendered in UI E2E cell; run with partial pass renders `{passed}/{total}` — `source/Testurio.Web/src/components/RunHistoryTable/RunHistoryTable.test.tsx`

## Rationale

**`StepSummary` domain model first (T001, T002).** Both the pipeline (`ReportWriter`) and the portal API (`RunDetailResponse`) depend on this record. It belongs in `Testurio.Core` so it can be referenced from `Testurio.Pipeline.ReportWriter`, `Testurio.Api`, and `Testurio.Infrastructure` without circular dependencies. `ScenarioSummary` already lives in `ReportContent.cs`; extending it in-place avoids adding a new file for a small change.

**Pipeline logic before infrastructure (T003, T004, T005).** `ReportWriter.BuildScenarioSummaries` (T003) is the single place that maps raw `StepExecutionResult` objects to `StepSummary` records; once it sets `Steps` correctly on every `ScenarioSummary`, the enriched data flows automatically to both the Cosmos write path (via `TestResult.ScenarioResults`) and the PM comment renderer. Updating `PmCommentFormatter` (T004) is independent of `ReportWriter` but depends on `StepSummary` being stable (T001). Infrastructure round-trip (T005) is a defensive update — Cosmos is schema-less, so no migration is needed, but the repository deserialisation must be tested to ensure the nested array survives the round-trip.

**`ScenarioSummary` extension is additive and backward-compatible.** Existing `TestResult` documents in Cosmos have no `steps` field; System.Text.Json deserialises missing properties as their default value (`null` for a nullable reference type), satisfying AC-015. No data migration is required.

**Frontend type extension before component work (T006, T007).** TypeScript types must be updated before components can be written to compile without `any` casts. MSW mock handler (T007) must expose `steps` on test fixtures before component tests (T014–T016) can exercise the new rendering paths.

**`UiStepList` as a standalone component (T008) before `ScenarioCard` update (T009).** The step list is a self-contained, testable unit with no dependencies on `ScenarioCard`. Extracting it into its own component keeps `ScenarioCard` focused on expand/collapse orchestration and avoids a single oversized file.

**`RunHistoryTable` update (T010) is independent.** The `totalUiE2eScenarios` and `passedUiE2eScenarios` fields already exist on `RunHistoryItem` (feature 0011) — no API change is needed. This task only adds a conditional column to an existing component.

**Tests last, per QA rules (T012–T016).** Backend tests (T012, T013) exercise only logic that changes in this feature — `PmCommentFormatter` step rendering and `ReportWriter.BuildScenarioSummaries` step mapping. All other `ReportWriter` scenarios tested in 0030 remain unchanged. Frontend tests (T014–T016) are co-located with their components and cover all acceptance-criteria branches without live HTTP calls (MSW handles network requests).

**No API endpoint changes required.** `RunDetailResponse` wraps `IReadOnlyList<ScenarioSummary>` from `Testurio.Core`; since `ScenarioSummary` is extended with the `Steps` property (T002), the existing endpoint at `GET /v1/stats/projects/{projectId}/runs/{runId}` will automatically serialise the new field. No new endpoint or DTO change is needed.

**No new Cosmos container, migration, or Bicep change.** `StepSummary` is a nested object stored inline in the `ScenarioResults` array on each `TestResult` document. The Cosmos container schema is schema-less — the new field is stored and retrieved transparently.

**Cross-feature dependencies.**
- Feature 0029 — `StepExecutionResult` (the source data for `StepSummary`); `PlaywrightExecutor` must be fully implemented for integration testing.
- Feature 0030 — `ReportWriter` and `PmCommentFormatter` are the pipeline files modified in T003 and T004; these files are stable (all phases Complete) and the changes here are additive.
- Feature 0011 — `RunHistoryItem.totalUiE2eScenarios` / `passedUiE2eScenarios` already present; `ScenarioSummary` / `RunDetailResponse` already consumed by `RunDetailPanel` and `ScenarioCard`.
- Feature 0009 — `{{scenarios}}` placeholder expansion in the report template is extended (T004) to include the per-step table; this is a backwards-compatible addition — existing templates continue to work.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Blob Storage clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | Services, formatters, pipeline stage implementations — pipeline projects |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Worker]` | Job processors, queue managers, pipeline steps — `Testurio.Worker` |
| `[Config]` | DI registration, app configuration, environment settings — any project |
| `[UI]` | Next.js pages, components, API clients, hooks, MSW handlers, i18n keys — `Testurio.Web` |
| `[Test]` | Unit and integration test files — `tests/` and co-located `.test.tsx` files |
