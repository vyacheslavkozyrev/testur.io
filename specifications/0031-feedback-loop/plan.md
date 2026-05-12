# Implementation Plan — Test Result Feedback Loop (0031)

## Tasks

- [ ] T001 [Domain] Define `ITestMemoryRepository` interface with `UpdatePassRateAsync(string entryId, string userId, double passRate, int runCount, bool isDeleted, DateTimeOffset lastUsedAt, CancellationToken ct) → Task` — `source/Testurio.Core/Interfaces/ITestMemoryRepository.cs`
- [ ] T002 [Domain] Define `IFeedbackLoop` interface (`RunAsync(ExecutionResult executionResult, MemoryRetrievalResult memoryRetrievalResult, TestRun testRun, CancellationToken ct) → Task`) — `source/Testurio.Core/Interfaces/IFeedbackLoop.cs`
- [ ] T003 [Infra] Add `UpdatePassRateAsync` to `TestMemoryRepository` (uses Cosmos `PatchItemAsync` with `userId` partition key to atomically update `passRate`, `runCount`, `isDeleted`, and `lastUsedAt` fields) — `source/Testurio.Infrastructure/Cosmos/TestMemoryRepository.cs`
- [ ] T004 [Infra] Register `ITestMemoryRepository → TestMemoryRepository` in infrastructure DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T005 [App] Create `FeedbackLoopOptions` record (`double Alpha = 0.8`) with `[Range(0.1, 0.99)]` data annotation — `source/Testurio.Pipeline.FeedbackLoop/FeedbackLoopOptions.cs`
- [ ] T006 [App] Implement `FeedbackLoop` (`IFeedbackLoop`; no-op guard when scenarios or executor results are empty; compute overall outcome from `ExecutionResult`; for each `TestMemoryEntry` in `MemoryRetrievalResult.Scenarios`: load from Cosmos, skip if `isDeleted`, apply EMA, increment `runCount` on pass only, set `isDeleted = true` when `passRate < 0.5 && runCount >= 5`, write `UpdatePassRateAsync`; entries updated sequentially; all exceptions caught — log structured warning including `runId`, do not rethrow) — `source/Testurio.Pipeline.FeedbackLoop/FeedbackLoop.cs`
- [ ] T007 [Config] Bind `FeedbackLoop:Alpha` config section to `FeedbackLoopOptions` with `ValidateDataAnnotations().ValidateOnStart()`; register `IFeedbackLoop → FeedbackLoop` as scoped — `source/Testurio.Pipeline.FeedbackLoop/DependencyInjection.cs`
- [ ] T008 [Config] Add `"FeedbackLoop": { "Alpha": 0.8 }` to worker appsettings — `source/Testurio.Worker/appsettings.json`
- [ ] T009 [Worker] Wire `IFeedbackLoop.RunAsync(executionResult, memoryRetrievalResult, testRun, cancellationToken)` as stage 7 in `TestRunJobProcessor` immediately after stage 6 (`IReportWriter.WriteAsync`) returns successfully; do not wrap in try/catch — `FeedbackLoop` is self-contained and non-throwing by contract — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T010 [Test] Unit tests for `FeedbackLoop` (empty scenarios → no Cosmos calls; empty executor results → no Cosmos calls; all API pass → `passRate` updated up via EMA, `runCount` incremented, `isDeleted` false; any scenario fails → `passRate` updated down, `runCount` unchanged; `passRate < 0.5` and `runCount >= 5` → `isDeleted = true` and soft-delete log emitted; `passRate < 0.5` and `runCount < 5` → `isDeleted` remains false; entry already `isDeleted` → skipped, no write; Cosmos write throws → warning logged, no rethrow; `CancellationToken` forwarded to Cosmos call) — `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopTests.cs`
- [ ] T011 [Test] Integration tests for stage 7 via `TestRunJobProcessor` (pre-seeded passing entry reused → `passRate` increased in Cosmos; entry reused on fail run → `passRate` decreased, `runCount` unchanged; entry crosses soft-delete threshold → `isDeleted = true` persisted; Cosmos write throws → pipeline continues to stage 8 without error; empty `MemoryRetrievalResult` → no Cosmos writes, pipeline continues) — `tests/Testurio.IntegrationTests/Pipeline/FeedbackLoopIntegrationTests.cs`

## Rationale

**`ITestMemoryRepository` before `IFeedbackLoop`.** T001 must be defined first because `IFeedbackLoop` (T002) does not reference `ITestMemoryRepository` directly — `FeedbackLoop` does — but establishing the repository contract in `Testurio.Core` before any implementation task makes both the repository and pipeline code reviewable against the same interface. Feature 0027 implemented `TestMemoryRepository` as a concrete class without a `Testurio.Core` interface; this feature introduces that interface to make `FeedbackLoop` independently testable by injecting a mock repository. Feature 0032 (MemoryWriter) will extend `ITestMemoryRepository` with `UpsertAsync`.

**Infra before pipeline.** `UpdatePassRateAsync` (T003) must exist in the infrastructure layer before `FeedbackLoop` (T006) can call it at runtime. The DI registration (T004) must exist before the worker resolves `ITestMemoryRepository` from the container. Cosmos's `PatchItemAsync` is used rather than a full document replace to minimise write contention and network payload — only the four changed fields are transmitted.

**`FeedbackLoopOptions` before `FeedbackLoop`.** T005 is a simple configuration record; it must be defined before T006 reads `Alpha` from it. Validation at startup (T007) ensures a misconfigured `Alpha` value (e.g., `1.5`) fails the process immediately rather than silently using an out-of-range value on the first run.

**Sequential entry updates by design.** The top-3 retrieval results from stage 3 are per test type. If both `api` and `ui_e2e` are enabled, the same `MemoryRetrievalResult` may theoretically contain the same entry ID returned by different cosine searches (unlikely at top-3 but not impossible with a small memory corpus). Sequential writes ensure the second write reads the state left by the first rather than racing against it. Given the cap of at most 6 entries (3 per type, 2 types), sequential iteration has no measurable latency impact.

**Non-throwing contract enforced at the implementation level, not the worker level.** The `catch-all` is inside `FeedbackLoop.cs` (T006), not in `TestRunJobProcessor` (T009). This design means `TestRunJobProcessor` does not need a dedicated try/catch block for stage 7 — the stage is self-contained. If the catch were in the worker, any refactor that moves stage 7 logic would need to replicate the non-throwing pattern. Keeping the contract in the implementation ensures it travels with the class regardless of caller.

**No `TestRun` mutation.** Unlike stage 6 (`ReportWriter`), which updates `TestRun.Status` and `TestRun.PmCommentId`, stage 7 does not modify the `TestRun` record. It reads `testRun.RunId` for logging only. This keeps stage 7 side-effects localised to the `TestMemory` container.

**Worker wiring last among production tasks.** T009 modifies `TestRunJobProcessor`, the central orchestrator. Changing it before the service and its DI registration are stable would require re-touching the file. Implementing T009 after T001–T008 ensures a single, clean edit to the processor.

**Cross-feature dependencies.** This feature depends on:
- Feature 0025 — `TestRun` entity (read for `RunId` logging)
- Feature 0026 — `TestRunJobProcessor` scaffolding, `ExecutionResult` type
- Feature 0027 — `TestMemoryEntry` (with `id` field), `MemoryRetrievalResult`, `TestMemoryRepository` concrete class
- Feature 0029 — `ApiScenarioResult`, `UiE2eScenarioResult` (used to compute overall outcome)
- Feature 0030 — stage 6 must complete before stage 7 runs; 0030's AC-024 establishes that `executionResult` and `testRun` are available in scope at this point

Feature 0032 (MemoryWriter) depends on `ITestMemoryRepository` defined here (T001) to add `UpsertAsync` in a later task.

**No UI tasks.** FeedbackLoop is a pure backend pipeline stage. Memory quality signals are surfaced in the portal via feature 0040 (Memory Scenario Viewer, post-MVP), which reads `passRate` and `runCount` directly from the `TestMemory` container. No frontend changes are required by this feature.

**Tests last, per QA rules.** Unit tests (T010) mock `ITestMemoryRepository` to exercise all acceptance-criteria branches — including the EMA formula, the `runCount` gating, and the soft-delete threshold — without live Cosmos calls. Integration tests (T011) run through `TestRunJobProcessor` against the Cosmos emulator and verify durable state changes per scenario.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Blob Storage clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | Services, API clients, DTOs — `Testurio.Api` or pipeline projects |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Worker]` | Job processors, queue managers, pipeline steps — `Testurio.Worker` |
| `[Plugin]` | Semantic Kernel plugins — `Testurio.Plugins` |
| `[Config]` | DI registration, app configuration, environment settings — any project |
| `[UI]` | Next.js pages, components, API clients, hooks — `Testurio.Web` |
| `[Test]` | Unit and integration test files — `tests/` |
