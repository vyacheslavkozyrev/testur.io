# Implementation Plan — Automated API Test Execution (0003)

## Tasks

- [x] T001 [Domain] Create `StepResult` entity — `source/Testurio.Core/Entities/StepResult.cs`
- [x] T002 [Domain] Create `StepStatus` enum (Passed / Failed / Error / Timeout / Skipped) — `source/Testurio.Core/Enums/StepStatus.cs`
- [x] T003 [Domain] Add `IStepResultRepository` interface — `source/Testurio.Core/Repositories/IStepResultRepository.cs`
- [x] T004 [Infra] Implement `StepResultRepository` (Cosmos DB) — `source/Testurio.Infrastructure/Cosmos/StepResultRepository.cs`
- [x] T005 [Infra] Implement `KeyVaultCredentialClient` (resolve Bearer token from project Key Vault secret reference) — `source/Testurio.Infrastructure/KeyVault/KeyVaultCredentialClient.cs`
- [x] T006 [Infra] Update DI registration with new repository and credential client — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T007 [Plugin] Implement `ResponseSchemaValidator` (validate HTTP status code and response body schema against expected values) — `source/Testurio.Plugins/TestExecutorPlugin/ResponseSchemaValidator.cs`
- [x] T008 [Plugin] Implement `TestExecutorPlugin` (build HTTP requests from step definitions, inject Bearer token, enforce 10s timeout, invoke validator, record per-step outcome) — `source/Testurio.Plugins/TestExecutorPlugin/TestExecutorPlugin.cs`
- [ ] T009 [Worker] Implement `ApiTestExecutionStep` (for each scenario dispatch all steps in parallel, collect results, persist, aggregate run status, trigger report step) — `source/Testurio.Worker/Steps/ApiTestExecutionStep.cs`
- [ ] T010 [Worker] Integrate `ApiTestExecutionStep` into `TestRunJobProcessor` after `ScenarioGenerationStep` — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T011 [Test] Unit tests for `ResponseSchemaValidator` (status mismatch, schema mismatch, both pass) — `tests/Testurio.UnitTests/Plugins/ResponseSchemaValidatorTests.cs`
- [ ] T012 [Test] Unit tests for `TestExecutorPlugin` (happy path, timeout, missing auth, malformed step definition) — `tests/Testurio.UnitTests/Plugins/TestExecutorPluginTests.cs`
- [ ] T013 [Test] Unit tests for `ApiTestExecutionStep` (run status aggregation, all-pass and partial-fail scenarios) — `tests/Testurio.UnitTests/Steps/ApiTestExecutionStepTests.cs`
- [ ] T014 [Test] Integration tests for the full pipeline (trigger → generate → execute) — `tests/Testurio.IntegrationTests/Pipeline/TestRunPipelineTests.cs`

## Rationale

**Domain before infrastructure.** `StepResult` (T001) and `StepStatus` (T002) define the output contract of this feature and the primary input to feature 0004. `IStepResultRepository` (T003) follows immediately so the domain layer is fully defined before any infrastructure code is written — consistent with the pattern in features 0001 and 0002.

**`KeyVaultCredentialClient` in infrastructure.** Per the architecture, credentials are never stored in Cosmos DB — only a Key Vault secret reference lives in the project document. The credential client (T005) resolves that reference at execution time. Placing it in `Testurio.Infrastructure` keeps Key Vault access isolated from the plugin and worker layers. It must exist before `TestExecutorPlugin` (T008) can read the Bearer token.

**`ResponseSchemaValidator` before `TestExecutorPlugin`.** The validator (T007) is a pure, stateless dependency of the executor plugin (T008). Implementing it first makes the validation logic independently testable and keeps `TestExecutorPlugin` focused solely on HTTP dispatch and result recording.

**`TestExecutorPlugin` before `ApiTestExecutionStep`.** The plugin owns all HTTP mechanics — request construction, auth injection, timeout enforcement, and response validation. The step (T009) only orchestrates: it calls the plugin per scenario, collects results, aggregates run status, and triggers the next pipeline stage. Separating them mirrors the `StoryParserPlugin` / `ScenarioGenerationStep` split established in feature 0002.

**Worker integration last before tests.** T010 extends `TestRunJobProcessor` to chain `ApiTestExecutionStep` after `ScenarioGenerationStep`. This is the highest-risk edit — touching the processor that already owns the queue dispatch logic from 0001 and the generation step wired in by 0002. Doing it last, after the new step is fully implemented and unit-tested, minimises the chance of destabilising earlier pipeline stages.

**Integration test covers the full pipeline.** T014 exercises the end-to-end flow from a Service Bus message (feature 0001) through scenario generation (feature 0002) to step execution and result persistence (feature 0003). This is the first point in the codebase where all three features are wired together; catching integration issues here avoids surprises when feature 0004 is added.

**Cross-feature dependencies.** This feature depends on features 0001 and 0002 being implemented first: `TestRun` / `ITestRunRepository` (0001) are used to set the final run status (AC-017); `TestScenario` / `TestScenarioStep` / `ITestScenarioRepository` (0002) are loaded by `ApiTestExecutionStep` to drive execution (AC-001). Feature 0004 depends on this feature: `StepResult` and `IStepResultRepository` are the primary data source for the report writer. No changes to `StepResult` or `StepStatus` should be made after 0004 planning begins without an amendment here.

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
