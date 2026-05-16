# Implementation Plan — Agent Router (0026)

## Tasks

- [x] T001 [Domain] Create `TestType` enum (`api`, `ui_e2e`) — `source/Testurio.Core/Enums/TestType.cs`
- [x] T002 [Domain] Create `AgentRouterResult` record (`TestType[] ResolvedTestTypes`, `string ClassificationReason`) — `source/Testurio.Core/Models/AgentRouterResult.cs`
- [x] T003 [Domain] Create `ITestGeneratorAgent` marker interface (concrete implementations provided in 0028) — `source/Testurio.Core/Interfaces/ITestGeneratorAgent.cs`
- [x] T004 [Domain] Create `ITestGeneratorFactory` interface (`Create(TestType) → ITestGeneratorAgent`) — `source/Testurio.Core/Interfaces/ITestGeneratorFactory.cs`
- [x] T005 [Domain] Create `IAgentRouter` interface (`RouteAsync(ParsedStory, ProjectConfig, TestRun, CancellationToken) → Task<AgentRouterResult>`) — `source/Testurio.Core/Interfaces/IAgentRouter.cs`
- [ ] T006 [Domain] Extend `TestRun` entity with `ResolvedTestTypes` (`string[]`), `ClassificationReason` (`string`), and `Skipped` variant on the existing run-status type — `source/Testurio.Core/Entities/TestRun.cs`
- [ ] T007 [Infra] Update `TestRunRepository` Cosmos write path to persist `resolvedTestTypes`, `classificationReason`, and the `Skipped` status — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`
- [ ] T008 [App] Implement `StoryClassifier` (calls Claude `claude-opus-4-7` with adaptive thinking; parses response JSON into `TestType[]` and `classificationReason`) — `source/Testurio.Pipeline.AgentRouter/StoryClassifier.cs`
- [ ] T009 [App] Implement `SkipCommentPoster` (posts skip comment to ADO or Jira based on run context; fire-and-forget, error-swallowing) — `source/Testurio.Pipeline.AgentRouter/SkipCommentPoster.cs`
- [ ] T010 [App] Implement `TestGeneratorFactory` (`ITestGeneratorFactory`; resolves concrete generators by keyed DI; throws `ArgumentOutOfRangeException` for unrecognised type) — `source/Testurio.Pipeline.AgentRouter/TestGeneratorFactory.cs`
- [ ] T011 [App] Implement `AgentRouterService` (`IAgentRouter`; classifier → project-config filter → factory build list or skip path; returns `AgentRouterResult`) — `source/Testurio.Pipeline.AgentRouter/AgentRouterService.cs`
- [ ] T012 [Config] Register `AgentRouterService` as `IAgentRouter`, `TestGeneratorFactory` as `ITestGeneratorFactory`, and their dependencies in DI — `source/Testurio.Pipeline.AgentRouter/DependencyInjection.cs`
- [ ] T013 [Worker] Wire `IAgentRouter` into `TestRunJobProcessor`; invoke `RouteAsync` after `IStoryParser`; stop pipeline and write `Skipped` status when result is empty; write routing metadata to run record on all paths — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T014 [Test] Unit tests for `StoryClassifier` (`api`-only result; `ui_e2e`-only result; both types; empty JSON array from Claude; Claude API error → exception propagated) — `tests/Testurio.UnitTests/Pipeline/StoryClassifierTests.cs`
- [ ] T015 [Test] Unit tests for `TestGeneratorFactory` (returns `ApiTestGeneratorAgent` for `api`; returns `UiE2eTestGeneratorAgent` for `ui_e2e`; throws `ArgumentOutOfRangeException` for unknown value) — `tests/Testurio.UnitTests/Pipeline/TestGeneratorFactoryTests.cs`
- [ ] T016 [Test] Unit tests for `AgentRouterService` (classified types pass project-config filter; type absent from project config is excluded; empty after filter → skip comment posted, `Skipped` status set; comment-post failure → pipeline continues; two types → two generators returned) — `tests/Testurio.UnitTests/Pipeline/AgentRouterServiceTests.cs`
- [ ] T017 [Test] Integration tests for the full routing stage via `TestRunJobProcessor` (classifiable story → generators built end-to-end; unclassifiable → `Skipped` status + comment posted; both types resolved → two generator instances forwarded to stage 4) — `tests/Testurio.IntegrationTests/Pipeline/AgentRouterIntegrationTests.cs`

## Rationale

**Domain contracts before everything else.** `TestType` (T001) and `AgentRouterResult` (T002) are the vocabulary of the entire stage. `ITestGeneratorAgent` (T003) and `ITestGeneratorFactory` (T004) define the extension point that feature 0028 (Generators) must satisfy — those features cannot begin implementation until T003–T004 are merged and stable. `IAgentRouter` (T005) gives the worker a clean abstraction over the implementation. `TestRun` extensions (T006) are domain entities; they must be defined before the infrastructure layer can serialise them.

**Infrastructure before the pipeline project.** The `TestRunRepository` write path (T007) must be updated before the worker integration (T013) attempts to persist routing metadata. Updating the Cosmos write path is additive — adding new fields to the JSON document is backwards-compatible in Cosmos DB's schema-less model.

**Pipeline-internal ordering: classifier → skip poster → factory → orchestrator.** `StoryClassifier` (T008) has no intra-project dependencies and is the core AI call; it is implemented first. `SkipCommentPoster` (T009) is independent of classifier output — it only needs run context (PM tool type, ticket reference) — so it can be developed in parallel but is listed sequentially for clarity. `TestGeneratorFactory` (T010) depends on keyed DI registrations for `ITestGeneratorAgent` implementations being available at runtime; it throws `ArgumentOutOfRangeException` for any unrecognised type so failures are loud. `AgentRouterService` (T011) orchestrates all three components and is therefore implemented last.

**DI registration before worker integration.** T012 must be complete before T013 so `TestRunJobProcessor` can resolve `IAgentRouter` and `ITestGeneratorFactory` from the container.

**Cross-feature dependencies.** This feature depends on feature 0025 (`ParsedStory`, `TestRun` entity, `AnthropicClient` singleton DI registration) being merged first — none of the pipeline tasks can proceed without the `ParsedStory` input contract. Conversely, features 0027 (MemoryRetrieval) and 0028 (Generators) depend on `ITestGeneratorAgent` (T003) and `AgentRouterResult` (T002) from this feature; they must not begin implementation until those types are merged.

**No UI tasks.** Routing metadata (`resolvedTestTypes`, `classificationReason`) is surfaced through the existing `/api/stats` run-detail endpoint by virtue of the `TestRun` entity carrying the new fields; no new portal pages or API endpoints are required. AC-018 is satisfied by the stats endpoint already returning the full `TestRun` document.

**Tests last, per QA rules.** Unit tests (T014–T016) cover every acceptance-criteria path without live external calls; integration tests (T017) exercise the full stage through `TestRunJobProcessor` using mocked Anthropic and PM tool clients.

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
