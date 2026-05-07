# Implementation Plan — Story-Driven Test Scenario Generation (0002)

## Tasks

- [x] T001 [Domain] Create `TestScenario` entity — `source/Testurio.Core/Entities/TestScenario.cs`
- [x] T002 [Domain] Create `TestScenarioStep` value object — `source/Testurio.Core/Models/TestScenarioStep.cs`
- [x] T003 [Domain] Add `ITestScenarioRepository` interface — `source/Testurio.Core/Repositories/ITestScenarioRepository.cs`
- [x] T004 [Infra] Implement `TestScenarioRepository` (Cosmos DB) — `source/Testurio.Infrastructure/Cosmos/TestScenarioRepository.cs`
- [x] T005 [Infra] Implement `JiraStoryClient` (fetch story description and AC from Jira REST API) — `source/Testurio.Infrastructure/Jira/JiraStoryClient.cs`
- [x] T006 [Infra] Update DI registration with new repository and Jira client — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T007 [Config] Configure Semantic Kernel with Anthropic Claude connector — `source/Testurio.Worker/DependencyInjection.cs`
- [x] T008 [Plugin] Implement `StoryParserPlugin` (extract description and AC from Jira story payload) — `source/Testurio.Plugins/StoryParserPlugin/StoryParserPlugin.cs`
- [x] T009 [Plugin] Implement `TestGeneratorPlugin` (call Claude via Semantic Kernel, hardcoded system prompt, deserialise response to `TestScenario` list) — `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs`
- [x] T010 [Worker] Implement `ScenarioGenerationStep` (orchestrate: fetch story → parse → generate → persist → trigger next step or fail run) — `source/Testurio.Worker/Steps/ScenarioGenerationStep.cs`
- [x] T011 [Worker] Integrate `ScenarioGenerationStep` into `TestRunJobProcessor` after queue dispatch — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T012 [Test] Unit tests for `StoryParserPlugin` — `tests/Testurio.UnitTests/Plugins/StoryParserPluginTests.cs`
- [ ] T013 [Test] Unit tests for `TestGeneratorPlugin` — `tests/Testurio.UnitTests/Plugins/TestGeneratorPluginTests.cs`
- [ ] T014 [Test] Unit tests for `ScenarioGenerationStep` (including failure and empty-response paths) — `tests/Testurio.UnitTests/Steps/ScenarioGenerationStepTests.cs`

## Rationale

**Domain before everything.** `TestScenario` (T001) and `TestScenarioStep` (T002) are the output contracts of this feature and the input contracts of feature 0003. Defining them first, alongside `ITestScenarioRepository` (T003), ensures the domain layer has no dependency on infrastructure or plugins — a pattern established in feature 0001.

**Infrastructure before plugins.** `TestScenarioRepository` (T004) persists generated scenarios; `JiraStoryClient` (T005) fetches story content from Jira. Both are dependencies of `ScenarioGenerationStep` (T010) and must be available before the worker orchestration layer is built. DI registration (T006) follows all infrastructure implementations.

**Semantic Kernel configuration before plugins.** The Claude connector (T007) must be registered in the worker's DI container before `TestGeneratorPlugin` (T009) can resolve `IChatCompletionService`. Configuring it as a dedicated step makes the LLM provider swap (Claude → vLLM) a single-file change when moving to MVP.

**`StoryParserPlugin` before `TestGeneratorPlugin`.** The parser (T008) extracts the structured description and AC fields that the generator (T009) receives as input. Implementing the parser first makes the generator's input contract concrete and testable in isolation.

**Plugins before worker step.** `ScenarioGenerationStep` (T010) orchestrates both plugins and the repository — it cannot be written until all three dependencies exist. T011 wires the step into the existing `TestRunJobProcessor` from feature 0001; modifying the processor last minimises risk of destabilising the already-defined queue dispatch logic.

**Cross-feature dependencies.** This feature depends on feature 0001 being implemented first: `TestRun` and `ITestRunRepository` are used by `ScenarioGenerationStep` to update run status on failure (AC-011, AC-012, AC-013) and to transition the run to the execution phase (AC-009). Feature 0003 depends on this feature: `TestScenario`, `TestScenarioStep`, and `ITestScenarioRepository` are the primary inputs to the test executor. No changes to these domain types should be made after 0003 planning begins without an amendment here.

**Tests last.** The two plugins and the orchestrating step are each tested in isolation (T012–T014), covering the happy path, the empty-response failure (AC-006), and the Claude API error failure (AC-011). Integration-level coverage of the full pipeline is provided by feature 0003's tests.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Service Bus clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | Services, API clients, DTOs — `Testurio.Api` |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Worker]` | Job processors, queue managers, pipeline steps — `Testurio.Worker` |
| `[Plugin]` | Semantic Kernel plugins — `Testurio.Plugins` |
| `[Config]` | DI registration, app configuration, environment settings — any project |
| `[UI]` | Next.js pages, components, API clients, hooks — `Testurio.Web` |
| `[Test]` | Unit and integration test files — `tests/` |
