# Implementation Plan — Test Generator Agents — API & UI E2E (0028)

## Tasks

- [x] T001 [Domain] Create `ApiTestScenario` record and `Assertion` hierarchy (`StatusCodeAssertion`, `JsonPathAssertion`, `HeaderAssertion`) — `source/Testurio.Core/Models/ApiTestScenario.cs`
- [x] T002 [Domain] Create `UiE2eTestScenario` record and `UiStep` hierarchy (`NavigateStep`, `ClickStep`, `FillStep`, `AssertVisibleStep`, `AssertTextStep`, `AssertUrlStep`) — `source/Testurio.Core/Models/UiE2eTestScenario.cs`
- [x] T003 [Domain] Create `GeneratorResults` record (`IReadOnlyList<ApiTestScenario> ApiScenarios`, `IReadOnlyList<UiE2eTestScenario> UiE2eScenarios`) — `source/Testurio.Core/Models/GeneratorResults.cs`
- [x] T004 [Domain] Create `PromptTemplate` record (`id`, `templateType`, `version`, `systemPrompt`, `generatorInstruction`, `maxScenarios`) — `source/Testurio.Core/Models/PromptTemplate.cs`
- [x] T005 [Domain] Create `GeneratorContext` record (`ParsedStory`, `MemoryRetrievalResult`, `ProjectConfig`, `PromptTemplate`, `Guid TestRunId`) — `source/Testurio.Core/Models/GeneratorContext.cs`
- [x] T006 [Domain] Extend `ITestGeneratorAgent` (stubbed in 0026 T003) with `GenerateAsync(GeneratorContext context, CancellationToken ct) → Task<GeneratorResults>` — `source/Testurio.Core/Interfaces/ITestGeneratorAgent.cs`
- [x] T007 [Domain] Create `IPromptTemplateRepository` interface (`GetAsync(string templateType, CancellationToken) → Task<PromptTemplate>`) — `source/Testurio.Core/Interfaces/IPromptTemplateRepository.cs`
- [x] T008 [Domain] Create `TestGeneratorException` (`TestType testType`, `int Attempts`, `string LastRawResponse`, `Exception innerException`) — `source/Testurio.Core/Exceptions/TestGeneratorException.cs`
- [x] T009 [Domain] Extend `TestRun` entity (defined in 0026 T006) with `GenerationWarnings` (`string[]`, default empty array) — `source/Testurio.Core/Entities/TestRun.cs`
- [x] T010 [Infra] Implement `PromptTemplateRepository` (`IPromptTemplateRepository`; reads from `PromptTemplates` Cosmos container by `id = templateType`; throws `InvalidOperationException` when document not found) — `source/Testurio.Infrastructure/Cosmos/PromptTemplateRepository.cs`
- [x] T011 [Infra] Register `IPromptTemplateRepository` as `PromptTemplateRepository` in infrastructure DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T012 [Infra] Seed initial `PromptTemplate` documents (`api_test_generator` with `maxScenarios: 10`; `ui_e2e_test_generator` with `maxScenarios: 5`) in Cosmos startup seeder — `source/Testurio.Infrastructure/Seeding/PromptTemplateSeeder.cs`
- [x] T013 [Infra] Update `TestRunRepository` Cosmos write path to persist `generationWarnings` field — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`
- [x] T014 [App] Implement `PromptAssemblyService` (assembles 6-layer prompt in order: systemPrompt → memory examples → customPrompt → testingStrategy → parsedStory → generatorInstruction with `{{maxScenarios}}` substitution; omits empty optional blocks) — `source/Testurio.Pipeline.Generators/Services/PromptAssemblyService.cs`
- [x] T015 [App] Implement `ApiTestGeneratorAgent` (`ITestGeneratorAgent`; keyed DI key `api`; calls Claude `claude-opus-4-7` streaming with adaptive thinking; parses response into `IReadOnlyList<ApiTestScenario>`; retries up to 3 times on `JsonException` with correction prompt appended to message history; throws `TestGeneratorException` after 4 failed attempts; returns `GeneratorResults` with `UiE2eScenarios` empty) — `source/Testurio.Pipeline.Generators/ApiTestGeneratorAgent.cs`
- [x] T016 [App] Implement `UiE2eTestGeneratorAgent` (`ITestGeneratorAgent`; keyed DI key `ui_e2e`; same streaming + retry pattern as T015; parses response into `IReadOnlyList<UiE2eTestScenario>`; selector preference enforced in generator instruction; returns `GeneratorResults` with `ApiScenarios` empty) — `source/Testurio.Pipeline.Generators/UiE2eTestGeneratorAgent.cs`
- [x] T017 [Config] Register `ApiTestGeneratorAgent` and `UiE2eTestGeneratorAgent` as keyed `ITestGeneratorAgent` (`"api"`, `"ui_e2e"`); register `PromptAssemblyService` — `source/Testurio.Pipeline.Generators/DependencyInjection.cs`
- [x] T018 [Worker] Wire generator stage into `TestRunJobProcessor`: load prompt templates via `IPromptTemplateRepository` for all resolved test types; build one `GeneratorContext` per agent; launch both with `Task.WhenAll`; catch `TestGeneratorException` per agent and accumulate `GenerationWarnings`; merge results into combined `GeneratorResults`; persist warnings via `TestRunRepository` before invoking stage 5 — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [x] T019 [Test] Unit tests for `PromptAssemblyService` (all 6 layers present; memory block omitted when empty; customPrompt block omitted when null; `{{maxScenarios}}` substituted correctly; layer order verified) — `tests/Testurio.UnitTests/Pipeline/Generators/PromptAssemblyServiceTests.cs`
- [x] T020 [Test] Unit tests for `ApiTestGeneratorAgent` (valid JSON → `ApiScenarios` populated; first attempt invalid then valid → retried once; 4 consecutive invalid → `TestGeneratorException` thrown; warning logged on each retry; `UiE2eScenarios` always empty in returned result) — `tests/Testurio.UnitTests/Pipeline/Generators/ApiTestGeneratorAgentTests.cs`
- [x] T021 [Test] Unit tests for `UiE2eTestGeneratorAgent` (valid JSON → `UiE2eScenarios` populated; retry and throw paths mirror T020; selector formats validated in parsed output; `ApiScenarios` always empty in returned result) — `tests/Testurio.UnitTests/Pipeline/Generators/UiE2eTestGeneratorAgentTests.cs`
- [x] T022 [Test] Unit tests for `PromptTemplateRepository` (existing template type → returns document; missing template type → throws `InvalidOperationException`) — `tests/Testurio.UnitTests/Infrastructure/PromptTemplateRepositoryTests.cs`
- [ ] T023 [Test] Integration tests for the full generator stage via `TestRunJobProcessor` (both agents succeed → merged `GeneratorResults` forwarded to stage 5; one agent exhausts retries → `GenerationWarnings` written, empty list for that type, pipeline continues; template not found → run fails before either agent starts; cancellation token cancelled mid-stream → both Claude calls cancelled) — `tests/Testurio.IntegrationTests/Pipeline/GeneratorsIntegrationTests.cs`

## Rationale

**Domain contracts before everything else.** `ApiTestScenario` (T001) and `UiE2eTestScenario` (T002) are the primary output types of this feature and the primary input types of feature 0029 (ExecutorRouter) — they must be stable before any agent or executor code is written. `GeneratorResults` (T003) is the typed handoff between this stage and stage 5; it must exist before `ITestGeneratorAgent` can reference it. `PromptTemplate` (T004) and `GeneratorContext` (T005) are the input contract for both agents; they must precede any agent implementation. `ITestGeneratorAgent` (T006) extends the marker interface stubbed by feature 0026 — this is the single type that `ITestGeneratorFactory` (also 0026) returns, so its method must be defined here before the factory can be usefully exercised end-to-end.

**`IPromptTemplateRepository` in Core, not Infrastructure.** Defining it in `Testurio.Core` (T007) keeps both agent projects free from a direct Azure SDK dependency. Agents depend on the abstraction; `Testurio.Infrastructure` provides the concrete Cosmos implementation.

**`TestGeneratorException` before agents.** T008 must exist before T015 and T016 can `throw` and before T018 can `catch`. `TestRun.GenerationWarnings` (T009) is an entity field — it belongs in the domain layer and must be defined before `TestRunRepository` (T013) can serialise it.

**Infrastructure before pipeline projects.** `PromptTemplateRepository` (T010) and its DI registration (T011) must be in place before `TestRunJobProcessor` resolves `IPromptTemplateRepository` from the container. The Cosmos seeder (T012) runs at startup; without seed data the worker cannot load templates and all runs fail at T018. `TestRunRepository` (T013) is updated to persist the new `GenerationWarnings` field — this is additive in Cosmos's schema-less model and therefore non-breaking to existing documents.

**`PromptAssemblyService` before agents.** T014 is a pure assembly utility with no Claude dependency; both agents depend on it to build their prompt strings. Implementing it first allows agents (T015, T016) to focus solely on the Claude call and output parsing.

**Agents before DI, DI before worker.** `ApiTestGeneratorAgent` (T015) and `UiE2eTestGeneratorAgent` (T016) can be written independently in parallel — they share no code path. Both must exist before T017 registers them as keyed services. T017 must be complete before T018 wires the stage into `TestRunJobProcessor`, otherwise the worker cannot resolve the agents from the container.

**Worker last among implementation tasks.** T018 is the integration point that ties together the prompt template load, context construction, parallel execution, exception handling, result merge, and Cosmos persistence. Implementing it last avoids repeatedly changing the worker as its dependencies stabilise.

**Cross-feature dependencies.** This feature depends on:
- Feature 0025 (`ParsedStory` record) — required by `GeneratorContext` (T005) and `IMemoryRetrievalService` signature
- Feature 0026 (`ITestGeneratorAgent` marker interface to extend in T006; `ITestGeneratorFactory` keyed DI keys; `TestRunJobProcessor` structure; `TestType` enum)
- Feature 0027 (`MemoryRetrievalResult` and `TestMemoryEntry` — required by `GeneratorContext` T005 and prompt assembly T014)

Features 0029 (ExecutorRouter), 0031 (FeedbackLoop), and 0032 (MemoryWriter) all depend on `ApiTestScenario` (T001), `UiE2eTestScenario` (T002), and `GeneratorResults` (T003) being merged and stable before their implementation begins.

**No UI tasks.** Generator agents are pure backend pipeline components. No portal pages, API endpoints, or statistics fields are added by this feature. `GeneratorResults` is pipeline-internal state that is not persisted directly to Cosmos — individual `TestResult` records written by feature 0030 (ReportWriter) carry the execution outcomes derived from these scenarios.

**Tests last, per QA rules.** Unit tests (T019–T022) cover every acceptance-criteria path without live external calls using mocked `AnthropicClient` and `IPromptTemplateRepository`. Integration tests (T023) exercise the full stage through `TestRunJobProcessor` using mocked Anthropic streaming responses and the Cosmos emulator.

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
