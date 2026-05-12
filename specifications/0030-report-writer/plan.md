# Implementation Plan — AI-Powered Report Writer (0030)

## Tasks

- [ ] T001 [Domain] Create `ReportContent` record (`string Verdict`, `string Recommendation`, `IReadOnlyList<ScenarioSummary> ScenarioSummaries`) and `ScenarioSummary` record (`string ScenarioId`, `string Title`, `bool Passed`, `long DurationMs`, `string? ErrorSummary`) — `source/Testurio.Core/Models/ReportContent.cs`
- [ ] T002 [Domain] Create `TestResult` entity (`string Id`, `string UserId`, `string ProjectId`, `string RunId`, `string StoryTitle`, `string Verdict`, `string Recommendation`, `int TotalApiScenarios`, `int PassedApiScenarios`, `int TotalUiE2eScenarios`, `int PassedUiE2eScenarios`, `long TotalDurationMs`, `string? PmCommentId`, `DateTimeOffset CreatedAt`) — `source/Testurio.Core/Entities/TestResult.cs`
- [ ] T003 [Domain] Define `IReportWriter` interface (`WriteAsync(ParsedStory story, ExecutionResult execution, ProjectConfig config, TestRun run, CancellationToken ct) → Task`) — `source/Testurio.Core/Interfaces/IReportWriter.cs`
- [ ] T004 [Domain] Define `ITestResultRepository` interface (`SaveAsync(TestResult result, CancellationToken ct) → Task`) — `source/Testurio.Core/Interfaces/ITestResultRepository.cs`
- [ ] T005 [Domain] Create `ReportWriterException` (message + optional inner exception) — `source/Testurio.Core/Exceptions/ReportWriterException.cs`
- [ ] T006 [Domain] Extend `TestRun` entity with `string? PmCommentId` (nullable, default `null`) and add `"ReportFailed"` to the `TestRunStatus` enum — `source/Testurio.Core/Entities/TestRun.cs`
- [ ] T007 [Infra] Implement `TestResultRepository` (`ITestResultRepository`; writes to Cosmos `TestResults` container partitioned by `userId`; uses `id` as document id) — `source/Testurio.Infrastructure/Cosmos/TestResultRepository.cs`
- [ ] T008 [Infra] Register `ITestResultRepository` as `TestResultRepository` in infrastructure DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T009 [Infra] Update `TestRunRepository` Cosmos write path to persist `pmCommentId` and the `"ReportFailed"` status value — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`
- [ ] T010 [App] Implement `PmCommentFormatter` (static helper; accepts `ReportContent`, `ParsedStory.title`, and `IReadOnlyList<UiE2eScenarioResult>`; renders verdict line, per-scenario blocks with diffs and screenshot links, recommendation line) — `source/Testurio.Pipeline.ReportWriter/PmCommentFormatter.cs`
- [ ] T011 [App] Implement `ReportWriter` (`IReportWriter`; calls Anthropic API with `ThinkingConfigAdaptive`; extracts and validates JSON from response; retries once on parse failure; validates `verdict` against raw `ExecutionResult`; calls `PmCommentFormatter`; calls `IPmToolClient.PostCommentAsync` (non-throwing on failure, logs warning); sets `TestRun.PmCommentId`; calls `ITestResultRepository.SaveAsync`; sets `TestRun.Status`; throws `ReportWriterException` on Cosmos failure) — `source/Testurio.Pipeline.ReportWriter/ReportWriter.cs`
- [ ] T012 [Config] Register `IReportWriter` as `ReportWriter` in pipeline DI — `source/Testurio.Pipeline.ReportWriter/DependencyInjection.cs`
- [ ] T013 [Worker] Wire `IReportWriter.WriteAsync` as stage 6 in `TestRunJobProcessor`; catch `ReportWriterException` → set `TestRun.Status = ReportFailed`, persist via `TestRunRepository`, rethrow; on success, forward `testRun` (with `PmCommentId` and `Status = Completed`) to stage 7 — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T014 [Test] Unit tests for `PmCommentFormatter` (PASSED verdict, FAILED verdict with API diffs, FAILED verdict with UI E2E step error and screenshot URI, all three recommendation labels, empty scenario lists) — `tests/Testurio.UnitTests/Pipeline/ReportWriter/PmCommentFormatterTests.cs`
- [ ] T015 [Test] Unit tests for `ReportWriter` (happy path: Claude returns valid JSON → comment posted → Cosmos written → status Completed; Claude parse fails once then succeeds on retry → report saved; Claude fails twice → `ReportWriterException` thrown; PM tool post-back throws → warning logged, Cosmos still written, `PmCommentId` null; Cosmos write fails → `ReportWriterException` thrown, status ReportFailed; `verdict` invariant mismatch → `ReportWriterException`; cancellation token forwarded to Claude call) — `tests/Testurio.UnitTests/Pipeline/ReportWriter/ReportWriterTests.cs`
- [ ] T016 [Test] Integration tests for the full stage via `TestRunJobProcessor` (both executor results present → `TestResult` persisted to Cosmos, PM comment posted; PM tool unavailable → `TestResult` still persisted, `PmCommentId` null; `ReportWriterException` → `TestRun.Status = ReportFailed`, message not settled) — `tests/Testurio.IntegrationTests/Pipeline/ReportWriterIntegrationTests.cs`

## Rationale

**Result and entity types before interfaces.** `ReportContent` + `ScenarioSummary` (T001) and `TestResult` (T002) are the data contracts everything else in this feature passes around. They must be stable before `IReportWriter` (T003) can declare its signature and before `ReportWriter.cs` (T011) can reference them. Defining them first also locks the handoff contract with features 0031 (FeedbackLoop) and 0032 (MemoryWriter), both of which read `TestRun` after stage 6 completes.

**Interfaces before implementations.** `IReportWriter` (T003) and `ITestResultRepository` (T004) live in `Testurio.Core`, keeping `Testurio.Pipeline.ReportWriter` and `Testurio.Infrastructure` free of cross-project references. `ReportWriterException` (T005) must exist before `ReportWriter.cs` can throw it and before `TestRunJobProcessor` can catch it.

**`TestRun` extensions in domain, not infrastructure.** `PmCommentId` and the `ReportFailed` status (T006) belong on the entity so pipeline code can set them without importing an Infrastructure type. `TestRunRepository` (T009) is updated after the entity change — Cosmos is schema-less so this is additive and non-breaking to existing documents.

**Infrastructure before pipeline.** `TestResultRepository` (T007) and its DI registration (T008) must be resolvable before `ReportWriter` (T011) can inject `ITestResultRepository`. Likewise, `TestRunRepository` (T009) is extended before T013 wires the worker stage, so `TestRun.PmCommentId` and `Status = ReportFailed` can be persisted durably.

**`PmCommentFormatter` before `ReportWriter`.** T010 is a pure, stateless formatting helper with no I/O; implementing it first lets T011 delegate all comment-rendering logic to it, keeping `ReportWriter` focused on orchestration. It also makes T014 (formatter unit tests) independent — they can be written and verified without mocking Claude or Cosmos.

**`ReportWriter` is the most complex task — implement last among production code.** T011 coordinates four concerns (Claude, formatter, PM tool, Cosmos). All its dependencies (T001–T010) must be stable before it is written, otherwise it will need to be partially revisited as interfaces or types change.

**DI registration (`[Config]`, T012) after implementation.** The DI file registers concrete types against interfaces; it cannot be written before the implementations exist.

**Worker wiring last among production tasks.** T013 (`TestRunJobProcessor` update) is the seam between this feature and the rest of the pipeline. Implementing it after all interfaces, implementations, and DI registrations are stable follows the pattern established across 0025–0029.

**Cross-feature dependencies.** This feature depends on:
- Feature 0025 — `ParsedStory` (input to stage 6), `IPmToolClient` (PM comment posting), and `TestRun` entity foundation
- Feature 0026 — `ProjectConfig` and `TestRunJobProcessor` scaffolding
- Feature 0027 — `TestRun.ExecutionWarnings` field (read by `ReportWriter` to determine recommendation)
- Feature 0028 — `GeneratorResults` (not directly consumed here, but upstream pipeline context)
- Feature 0029 — `ExecutionResult`, `ApiScenarioResult`, `UiE2eScenarioResult`, `StepExecutionResult.ScreenshotBlobUri` (primary inputs; must be implemented before 0030)

Feature 0031 (FeedbackLoop) and 0032 (MemoryWriter) depend on `TestRun.Status = "Completed"` being set by this stage before they run.

**No UI tasks.** `ReportWriter` is a pure backend pipeline stage. Test history and statistics are surfaced in the portal via feature 0011 (Per-Project Test History), which reads the `TestResult` documents written here. No frontend changes are required by this feature.

**Tests last, per QA rules.** Formatter tests (T014) are pure functions — no mocking required, fastest to write. `ReportWriter` unit tests (T015) mock Claude, `IPmToolClient`, and `ITestResultRepository` to exercise all acceptance-criteria branches. Integration tests (T016) run through `TestRunJobProcessor` with a real Cosmos emulator and a stubbed Anthropic client.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Blob Storage clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | Services, formatters, pipeline stage implementations — pipeline projects |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Worker]` | Job processors, queue managers, pipeline steps — `Testurio.Worker` |
| `[Plugin]` | Semantic Kernel plugins — `Testurio.Plugins` |
| `[Config]` | DI registration, app configuration, environment settings — any project |
| `[UI]` | Next.js pages, components, API clients, hooks — `Testurio.Web` |
| `[Test]` | Unit and integration test files — `tests/` |
