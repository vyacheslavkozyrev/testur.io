# Implementation Plan — Memory Writer Service (0032)

## Tasks

- [ ] T001 [Domain] Add `CrossProjectMemoryOptIn: bool` property (default `false`) to `Project` entity — `source/Testurio.Core/Entities/Project.cs`
- [ ] T002 [Domain] Add `UpsertAsync(TestMemoryEntry entry, CancellationToken ct) → Task` to `ITestMemoryRepository` — `source/Testurio.Core/Interfaces/ITestMemoryRepository.cs`
- [ ] T003 [Domain] Define `IMemoryWriter` interface (`WriteAsync(ExecutionResult executionResult, GeneratorResult generatorResult, ParsedStory parsedStory, TestRun testRun, CancellationToken ct) → Task`) — `source/Testurio.Core/Interfaces/IMemoryWriter.cs`
- [ ] T004 [Infra] Add `CrossProjectMemoryOptIn` field to `ProjectDocument` Cosmos model — `source/Testurio.Infrastructure/Cosmos/ProjectDocument.cs`
- [ ] T005 [Infra] Implement `UpsertAsync` on `TestMemoryRepository`: read existing entry by deterministic `id` and `userId` partition key; if found, preserve `passRate`, `runCount`, and `isDeleted` from the existing document and overwrite `scenarioText`, `storyEmbedding`, and `lastUsedAt`; call `UpsertItemAsync` with the merged document — `source/Testurio.Infrastructure/Cosmos/TestMemoryRepository.cs`
- [ ] T006 [App] Implement `MemoryWriter` class (`IMemoryWriter`): no-op guard exits immediately when any scenario result is not passed or both result lists are empty; call `IEmbeddingService.EmbedAsync` once for `parsedStory.StoryText`; for each API scenario build a `TestMemoryEntry` with `id = SHA256(userId + ":" + projectId + ":" + "api" + ":" + storyText).ToLowerHex()`, `testType = "api"`, `scenarioText = JsonSerializer.Serialize(scenario)`, `storyEmbedding = embedding`, `storyText = parsedStory.StoryText`, `userId = rawUserId`, `projectId = testRun.ProjectId`, `passRate = 1.0`, `runCount = 1`, `isDeleted = false`, `lastUsedAt = UtcNow`; repeat for UI E2E scenarios with `testType = "ui_e2e"`; call `ITestMemoryRepository.UpsertAsync` for each entry (which applies dedup and field-preservation internally); if `project.CrossProjectMemoryOptIn` repeat the writes with `userId = SHA256(rawUserId).ToLowerHex()` and `projectId = null`; catch-all wraps the entire body — log structured warning at `Warning` level including `runId` and exception message, do not rethrow — `source/Testurio.Pipeline.MemoryWriter/MemoryWriter.cs`
- [ ] T007 [Config] Register `IMemoryWriter → MemoryWriter` as scoped; reference `IEmbeddingService` and `ITestMemoryRepository` injected from DI — `source/Testurio.Pipeline.MemoryWriter/DependencyInjection.cs`
- [ ] T008 [Worker] Wire `IMemoryWriter.WriteAsync(executionResult, generatorResult, parsedStory, testRun, cancellationToken)` as stage 8 in `TestRunJobProcessor` immediately after stage 7 (`IFeedbackLoop.RunAsync`) completes; no surrounding try/catch — `MemoryWriter` is self-contained and non-throwing by contract — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T009 [Test] Unit tests for `MemoryWriter` (non-all-pass: any API fail → no embedding, no Cosmos calls; any UI E2E fail → no embedding, no Cosmos calls; empty results → no embedding, no Cosmos calls; all-pass API only → correct entries written with `testType = "api"`, no UI E2E entries; all-pass UI E2E only → correct entries with `testType = "ui_e2e"`; both types → entries for each scenario; deterministic `id` computed correctly per formula; `storyEmbedding` shared across all entries from same run; dedup hit → `passRate`/`runCount`/`isDeleted` preserved; cross-project opt-in false → single write per scenario; cross-project opt-in true → two writes per scenario with hashed userId and null projectId; `IEmbeddingService` throws → warning logged, no rethrow; `ITestMemoryRepository.UpsertAsync` throws → warning logged, no rethrow; `CancellationToken` forwarded to `EmbedAsync` and `UpsertAsync`) — `tests/Testurio.UnitTests/Pipeline/MemoryWriter/MemoryWriterTests.cs`
- [ ] T010 [Test] Integration tests for stage 8 via `TestRunJobProcessor` (all-pass run → entries persisted in Cosmos with correct fields; dedup: second all-pass run for same story → existing entry updated for `scenarioText`/`storyEmbedding`/`lastUsedAt`, `passRate`/`runCount`/`isDeleted` preserved; cross-project opt-in → both project-scoped and hashed-userId entries written; non-all-pass run → no entries written; `IEmbeddingService` throws → pipeline completes, Service Bus message acknowledged, no entries written; empty results → no entries, pipeline completes) — `tests/Testurio.IntegrationTests/Pipeline/MemoryWriterIntegrationTests.cs`

## Rationale

**Domain first: `Project` entity before `ITestMemoryRepository` and `IMemoryWriter`.** T001 adds `CrossProjectMemoryOptIn` to `Project` — both `MemoryWriter` (T006) and downstream DI (T007) read this field at runtime. Defining it first ensures no task introduces a compile dependency on a field that does not yet exist in `Testurio.Core`. T002 extends `ITestMemoryRepository` (defined in 0031-T001) rather than introducing a new interface; extending it here keeps all Cosmos `TestMemory` operations behind a single interface and makes the repository's test double in unit tests complete for both 0031 and 0032. T003 defines `IMemoryWriter` last among Domain tasks so the interface signature can reference `GeneratorResult` and `ParsedStory` types that are already in `Testurio.Core` from earlier features (0028 and 0001 respectively).

**`ProjectDocument` after `Project` entity.** T004 mirrors the domain field in the Cosmos model. The Cosmos model is a data-transfer concern in `Testurio.Infrastructure`; it must lag the domain entity so the canonical field definition resides in `Testurio.Core`.

**`UpsertAsync` implementation before `MemoryWriter`.** T005 implements the repository method that `MemoryWriter` (T006) calls. The read-then-write pattern in `UpsertAsync` is contained entirely inside `TestMemoryRepository`, keeping `MemoryWriter` free of Cosmos SDK details. The dedup key is a deterministic SHA-256 `id` (`userId + ":" + (projectId ?? "") + ":" + testType + ":" + storyText`), which allows `UpsertItemAsync` to locate the existing document without a separate query. Because `UpsertItemAsync` replaces the entire document, the implementation reads the current document first (one Cosmos read per scenario on a dedup hit), merges `passRate`, `runCount`, and `isDeleted` from it, and then issues the upsert. On a cold insert the read returns 404 and the provided defaults (`passRate = 1.0`, `runCount = 1`, `isDeleted = false`) are used. This keeps FeedbackLoop's quality-signal ownership intact: if FeedbackLoop already updated `passRate` for a reused entry earlier in the same pipeline run, that updated value survives the MemoryWriter upsert.

**Single embedding call for all scenarios in a run.** The `parsedStory.StoryText` is constant across all scenarios generated in one run. Calling `IEmbeddingService.EmbedAsync` once and reusing the vector for all entries avoids redundant API calls and ensures all entries for the same story share an identical embedding vector, which is important for cosine-distance consistency during retrieval.

**Cross-project writes are symmetric with project-scoped writes.** Each scenario produces up to two Cosmos writes: one project-scoped and one (opt-in) cross-project. The cross-project entry uses the same deterministic `id` formula with the hashed `userId` and an empty `projectId` segment, giving it its own independent dedup key. The hashed `userId` becomes the Cosmos partition key for cross-project entries — MemoryRetrieval (stage 3) queries only by the raw `userId` partition key, so these entries are naturally invisible to stage 3 in the MVP without any conditional filtering.

**Non-throwing contract at the implementation level.** The `catch-all` lives inside `MemoryWriter.cs` (T006), not in `TestRunJobProcessor` (T008). This mirrors the pattern established by FeedbackLoop (0031-T006): the stage is self-contained and its failure contract travels with the class regardless of its caller. `TestRunJobProcessor` requires no dedicated try/catch block for stage 8.

**Worker wiring last among production tasks.** T008 modifies `TestRunJobProcessor`, the central pipeline orchestrator. Touching it before T001–T007 are stable would require re-editing the file. A single, clean edit to the processor is only possible once the interface (`IMemoryWriter`, T003) and DI registration (T007) are in place.

**Tests after all production tasks.** Unit tests (T009) mock `IEmbeddingService` and `ITestMemoryRepository` to exercise every acceptance-criteria branch without live external calls, including the full matrix of pass/fail combinations, dedup behaviour, and exception swallowing. Integration tests (T010) run `TestRunJobProcessor` end-to-end against the Cosmos emulator to verify durable state changes, deduplication across runs, and the non-throwing contract at the pipeline level.

**Cross-feature dependencies.**

This feature depends on:
- Feature 0001 — `ParsedStory` (with `StoryText` field)
- Feature 0025 — `TestRun` entity (read for `RunId` logging, `ProjectId`)
- Feature 0026 — `TestRunJobProcessor` scaffolding, `ExecutionResult`, `GeneratorResult`
- Feature 0027 — `TestMemoryEntry`, `TestMemoryRepository` concrete class, `IEmbeddingService`
- Feature 0028 — `GeneratorResult` (typed scenario collections per test type)
- Feature 0029 — `ApiScenarioResult`, `UiE2eScenarioResult` (used in the all-pass guard)
- Feature 0031 — `ITestMemoryRepository` interface (T002 extends it); stage 7 must complete before stage 8 runs

**No UI tasks.** MemoryWriter is a pure backend pipeline stage. The portal-facing `CrossProjectMemoryOptIn` toggle is deferred; the field defaults to `false`, so the pipeline operates correctly without any UI change. Memory entries are surfaced in the portal via feature 0040 (Memory Scenario Viewer, post-MVP).

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
