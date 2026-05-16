# Implementation Plan — Memory Retrieval Service (0027)

## Tasks

- [x] T001 [Domain] Create `TestMemoryEntry` model (`id`, `userId`, `projectId`, `testType`, `storyText`, `scenarioText`, `passRate`, `runCount`, `lastUsedAt`, `isDeleted`) — `source/Testurio.Core/Models/TestMemoryEntry.cs`
- [x] T002 [Domain] Create `MemoryRetrievalResult` record (`IReadOnlyList<TestMemoryEntry> Scenarios`) — `source/Testurio.Core/Models/MemoryRetrievalResult.cs`
- [x] T003 [Domain] Create `IEmbeddingService` interface (`EmbedAsync(string text, CancellationToken) → Task<float[]>`) — `source/Testurio.Core/Interfaces/IEmbeddingService.cs`
- [x] T004 [Domain] Create `IMemoryRetrievalService` interface (`RetrieveAsync(ParsedStory story, ProjectConfig config, CancellationToken) → Task<MemoryRetrievalResult>`) — `source/Testurio.Core/Interfaces/IMemoryRetrievalService.cs`
- [x] T005 [Infra] Add `AzureOpenAIOptions` configuration class (`Endpoint`, `ApiKey`, `EmbeddingDeployment`) with `[Required]` data annotations — `source/Testurio.Infrastructure/Options/AzureOpenAIOptions.cs`
- [x] T006 [Infra] Implement `AzureOpenAIEmbeddingService` (`IEmbeddingService`; calls Azure OpenAI `text-embedding-3-small` via `Azure.AI.OpenAI` SDK; returns `float[1536]`) — `source/Testurio.Infrastructure/Embedding/AzureOpenAIEmbeddingService.cs`
- [ ] T007 [Infra] Implement `TestMemoryRepository` (Cosmos DiskANN vector query scoped to `userId + projectId` partition key; filters `isDeleted: false`; returns top-3 entries by cosine similarity) — `source/Testurio.Infrastructure/Cosmos/TestMemoryRepository.cs`
- [ ] T008 [Infra] Register `AzureOpenAIEmbeddingService` as `IEmbeddingService`, `TestMemoryRepository`, and `AzureOpenAIOptions` validated binding in infrastructure DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T009 [App] Implement `MemoryRetrievalService` (`IMemoryRetrievalService`; calls `IEmbeddingService` then `TestMemoryRepository`; on any exception catches, logs structured warning including `userId`, `projectId`, and run ID, and returns empty `MemoryRetrievalResult`) — `source/Testurio.Pipeline.MemoryRetrieval/MemoryRetrievalService.cs`
- [ ] T010 [Config] Register `MemoryRetrievalService` as `IMemoryRetrievalService` in pipeline DI — `source/Testurio.Pipeline.MemoryRetrieval/DependencyInjection.cs`
- [ ] T011 [Worker] Wire `IMemoryRetrievalService` into `TestRunJobProcessor`; invoke `RetrieveAsync` after `IAgentRouter` resolves generators and before stage 4 generators execute; pass `MemoryRetrievalResult` to each generator agent — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T012 [Test] Unit tests for `MemoryRetrievalService` (3 entries returned → all present in result; 0 entries → empty list, no warning emitted; embedding throws → empty result, warning logged with userId + projectId + runId; Cosmos query throws → empty result, warning logged; `isDeleted: true` entries excluded via repository filter) — `tests/Testurio.UnitTests/Pipeline/MemoryRetrievalServiceTests.cs`
- [ ] T013 [Test] Integration tests for the full retrieval stage via `TestRunJobProcessor` (pre-seeded memory entries → top-3 forwarded to generators; no entries → empty result, pipeline reaches stage 4; mocked `IEmbeddingService` and Cosmos emulator) — `tests/Testurio.IntegrationTests/Pipeline/MemoryRetrievalIntegrationTests.cs`

## Rationale

**Domain contracts first.** `TestMemoryEntry` (T001) is the shared data model consumed by this feature, feature 0031 (FeedbackLoop), and feature 0032 (MemoryWriter). Defining it here — before any infrastructure or pipeline code — gives those features a stable contract to depend on. `MemoryRetrievalResult` (T002) is the typed handoff between this stage and stage 4 generators; it must exist before `IMemoryRetrievalService` (T004) can reference it. `IEmbeddingService` (T003) is defined in `Testurio.Core` rather than `Testurio.Infrastructure` so the pipeline project depends on an abstraction, not a concrete Azure SDK class — this keeps `Testurio.Pipeline.MemoryRetrieval` infrastructure-ignorant and testable with a mock.

**Infrastructure before pipeline project.** `AzureOpenAIOptions` (T005) must be defined before `AzureOpenAIEmbeddingService` (T006) can read from it. Both embedding service and `TestMemoryRepository` (T007) are in `Testurio.Infrastructure` because they carry Azure SDK dependencies (`Azure.AI.OpenAI`, `Microsoft.Azure.Cosmos`) that must not bleed into the pipeline project. The infrastructure DI registration (T008) wires `IEmbeddingService` to its concrete implementation, making it injectable by the pipeline project with no direct Azure SDK reference.

**Pipeline service after infrastructure.** `MemoryRetrievalService` (T009) depends on `IEmbeddingService` and `TestMemoryRepository` being registered by T008. Its failure-handling contract (catch, warn, return empty) is implemented here rather than in the repository or embedding service, keeping those lower-level components simple and single-purpose.

**DI registration before worker integration.** `DependencyInjection.cs` (T010) must be loaded before `TestRunJobProcessor` (T011) resolves `IMemoryRetrievalService` from the container. The worker is modified last to avoid destabilising the existing stage 1 (StoryParser) and stage 2 (AgentRouter) wiring.

**Worker placement: after AgentRouter, before generators.** The architecture specifies stage 3 (MemoryRetrieval) runs after stage 2 (AgentRouter). This ordering matters because the generator list resolved by AgentRouter determines which generator agents will consume the memory result; the `MemoryRetrievalResult` is passed directly to those agents.

**Cross-feature dependencies.** This feature depends on feature 0025 (`ParsedStory` record) and feature 0026 (`ProjectConfig`, `TestRun`, worker job processor structure) being merged first — `IMemoryRetrievalService.RetrieveAsync` takes both as parameters. `TestMemoryEntry` (T001) and `IEmbeddingService` (T003) defined here are required by features 0031 (FeedbackLoop passRate updates) and 0032 (MemoryWriter upsert) — those features must not begin implementation until T001 and T003 are merged.

**No UI tasks.** Memory retrieval is a pure backend pipeline stage. No new portal pages, API endpoints, or run-record fields are added by this feature. The `MemoryRetrievalResult` is pipeline-internal state; it is not persisted to Cosmos and not surfaced in the statistics API.

**Tests last, per QA rules.** Unit tests (T012) cover every acceptance-criteria path — including cold start and both failure modes — without live external calls using mocked `IEmbeddingService` and `TestMemoryRepository`. Integration tests (T013) exercise the full stage through `TestRunJobProcessor` using the Cosmos emulator and a mocked embedding service.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Service Bus clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | Services, API clients, DTOs — `Testurio.Api` or pipeline projects |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Worker]` | Job processors, queue managers, pipeline steps — `Testurio.Worker` |
| `[Plugin]` | Semantic Kernel plugins — `Testurio.Plugins` |
| `[Config]` | DI registration, app configuration, environment settings — any project |
| `[UI]` | Next.js pages, components, API clients, hooks — `Testurio.Web` |
| `[Test]` | Unit and integration test files — `tests/` |
