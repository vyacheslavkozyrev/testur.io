# Implementation Plan — Execution Log Capture (0005)

## Tasks

- [x] T001 [Domain] Create `ExecutionLogEntry` entity (scenario ID, step index, step title, timestamp, HTTP method, URL, request headers, request body, response status, response headers, response body or blob URL, duration, error detail, truncation flag) — `source/Testurio.Core/Entities/ExecutionLogEntry.cs`
- [x] T002 [Domain] Add `IExecutionLogRepository` interface (persist, get by run ID, get by step ID, delete by run ID) — `source/Testurio.Core/Repositories/IExecutionLogRepository.cs`
- [x] T003 [Infra] Implement `BlobStorageClient` (upload response body, return URL; truncate and flag on upload failure) — `source/Testurio.Infrastructure/Blob/BlobStorageClient.cs`
- [ ] T004 [Infra] Implement `ExecutionLogRepository` (Cosmos DB — store inline body ≤10 KB, blob URL reference above threshold; resolve URL transparently on retrieval) — `source/Testurio.Infrastructure/Cosmos/ExecutionLogRepository.cs`
- [ ] T005 [Infra] Update DI registration with blob client and log repository — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T006 [Plugin] Implement `LogPersistenceService` (decide inline vs blob, upload to blob if needed, persist `ExecutionLogEntry`, absorb failures non-fatally and record system warning) — `source/Testurio.Plugins/TestExecutorPlugin/LogPersistenceService.cs`
- [ ] T007 [Plugin] Extend `TestExecutorPlugin` to capture raw request/response data per step and emit an `ExecutionLogEntry` to `LogPersistenceService` after each step completes — `source/Testurio.Plugins/TestExecutorPlugin/TestExecutorPlugin.cs`
- [ ] T008 [Plugin] Extend `ReportBuilderService` to accept an optional log section and append per-step log blocks (request, response, blob URL or truncation notice) as Jira markdown code blocks below the scenario breakdown — `source/Testurio.Plugins/ReportWriterPlugin/ReportBuilderService.cs`
- [ ] T009 [Worker] Extend `ApiTestExecutionStep` to collect log entries from `TestExecutorPlugin` and pass them to `LogPersistenceService` after each scenario completes — `source/Testurio.Worker/Steps/ApiTestExecutionStep.cs`
- [ ] T010 [Worker] Extend `ReportDeliveryStep` to load execution logs via `IExecutionLogRepository` and pass them to `ReportBuilderService` as the log section — `source/Testurio.Worker/Steps/ReportDeliveryStep.cs`
- [ ] T011 [Test] Unit tests for `LogPersistenceService` (inline path, blob path, blob upload failure with truncation, persistence failure is non-fatal) — `tests/Testurio.UnitTests/Plugins/LogPersistenceServiceTests.cs`
- [ ] T012 [Test] Unit tests for `ExecutionLogRepository` (persist, retrieve by run ID, retrieve by step ID, blob URL resolution) — `tests/Testurio.UnitTests/Infrastructure/ExecutionLogRepositoryTests.cs`
- [ ] T013 [Test] Unit tests for `ReportBuilderService` log section (inline body rendered, blob URL substituted, all runs include log blocks) — `tests/Testurio.UnitTests/Plugins/ReportBuilderServiceLogTests.cs`
- [ ] T014 [Test] Integration tests extending pipeline test to cover full flow with log capture and log-enriched report — `tests/Testurio.IntegrationTests/Pipeline/TestRunPipelineTests.cs`

## Rationale

**Domain first.** `ExecutionLogEntry` (T001) and `IExecutionLogRepository` (T002) define the log data contract. Everything else in this feature either produces or consumes these types. Defining them before infrastructure ensures no implementation detail leaks into the domain layer.

**`BlobStorageClient` before `ExecutionLogRepository`.** The repository's inline/blob routing logic (AC-005–AC-008) depends on the blob client being available through DI. Implementing the client (T003) before the repository (T004) allows the repository to be written with a real blob dependency rather than a placeholder.

**`LogPersistenceService` before extending `TestExecutorPlugin`.** The service (T006) owns the inline/blob decision and the non-fatal failure handling (AC-004, AC-008). Implementing it as a standalone collaborator first keeps the extension to `TestExecutorPlugin` (T007) minimal — the plugin only needs to capture data and hand it off. This also makes the persistence logic independently unit-testable (T011) without requiring a full HTTP execution context.

**`ReportBuilderService` extension before `ReportDeliveryStep` extension.** Feature 0004 designed `ReportBuilderService` to accept an optional log section parameter precisely for this extension point. T008 implements that parameter; T010 passes data into it. The builder change (T008) must precede the step change (T010) so the step has a working API to call.

**Worker extensions after plugin changes.** T009 and T010 modify `ApiTestExecutionStep` and `ReportDeliveryStep` respectively. Both are thin orchestrators — they call plugins and pass data along. The plugin changes (T006–T008) must be complete before the steps can reference the new APIs, keeping compilation order clear.

**Tests cover both new code and extended code.** T011 and T012 cover the new `LogPersistenceService` and `ExecutionLogRepository`. T013 specifically tests the log section extension to `ReportBuilderService` in isolation from its existing tests (0004/T009), keeping the test files focused. T014 extends the pipeline integration test for the fifth and final time, completing the full POC pipeline coverage: trigger → generate → execute + capture → report with logs.

**Cascade delete noted but not tasked.** AC-010 requires log entries and blobs to be deleted when the parent run record is deleted. No run deletion endpoint exists in POC scope. `IExecutionLogRepository` includes a `DeleteByRunIdAsync` method (T002) so the implementation is ready; it will be wired to the deletion path when that feature is built in MVP.

**Cross-feature dependencies.** This feature depends on features 0003 and 0004 being implemented first: `TestExecutorPlugin` (0003) is extended here to emit log entries — it must be stable before modification; `ReportBuilderService` (0004) is extended here to append the log section — its optional log parameter must match what was designed in 0004/T005. Features 0001 and 0002 are indirect dependencies via the pipeline but are not directly modified by this feature.

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
