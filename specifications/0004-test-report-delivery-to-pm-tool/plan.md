# Implementation Plan — Test Report Delivery to PM Tool (0004)

## Tasks

- [ ] T001 [Domain] Add `ReportDeliveryFailed` value to `TestRunStatus` enum — `source/Testurio.Core/Enums/TestRunStatus.cs`
- [ ] T002 [Infra] Move `JiraApiClient` from `Testurio.Api` to `Testurio.Infrastructure` and extend with `PostCommentAsync` — `source/Testurio.Infrastructure/Jira/JiraApiClient.cs`
- [ ] T003 [Infra] Update DI registration to expose `JiraApiClient` from infrastructure layer — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T004 [Infra] Remove or stub the now-relocated `JiraApiClient` reference in `Testurio.Api` — `source/Testurio.Api/Clients/JiraApiClient.cs`
- [ ] T005 [Plugin] Implement `ReportBuilderService` (assemble Jira markdown: summary header, failures section for failed runs, full scenario/step breakdown) — `source/Testurio.Plugins/ReportWriterPlugin/ReportBuilderService.cs`
- [ ] T006 [Plugin] Implement `ReportWriterPlugin` (load run, scenario, and step result data; call builder; post comment via `JiraApiClient`; handle delivery failure and update run status) — `source/Testurio.Plugins/ReportWriterPlugin/ReportWriterPlugin.cs`
- [ ] T007 [Worker] Implement `ReportDeliveryStep` (invoke `ReportWriterPlugin`, update run status on success or failure, log delivery error to Application Insights) — `source/Testurio.Worker/Steps/ReportDeliveryStep.cs`
- [ ] T008 [Worker] Integrate `ReportDeliveryStep` into `TestRunJobProcessor` after `ApiTestExecutionStep` — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T009 [Test] Unit tests for `ReportBuilderService` (passed run format, failed run format with failures section, timeout/error step rendering) — `tests/Testurio.UnitTests/Plugins/ReportBuilderServiceTests.cs`
- [ ] T010 [Test] Unit tests for `ReportWriterPlugin` (successful delivery, Jira 404, Jira auth error, delivery failure status update) — `tests/Testurio.UnitTests/Plugins/ReportWriterPluginTests.cs`
- [ ] T011 [Test] Integration tests extending 0003's pipeline test to cover the full flow (trigger → generate → execute → report) — `tests/Testurio.IntegrationTests/Pipeline/TestRunPipelineTests.cs`

## Rationale

**Amendment to 0001 domain first.** `TestRunStatus` (or the equivalent status field on `TestRun`) must include `ReportDeliveryFailed` before any code that sets it can be written. Adding the enum value first (T001) ensures all downstream tasks compile against the correct status contract.

**`JiraApiClient` relocation before plugins.** In feature 0001, `JiraApiClient` was placed in `Testurio.Api` because its only caller at that point was `JiraWebhookService`. `ReportWriterPlugin` runs in `Testurio.Worker` — a separate process that cannot reference `Testurio.Api`. Moving the client to `Testurio.Infrastructure` (T002–T004) makes it available to both projects without creating a circular dependency. This is done before any plugin code is written so T006 can resolve the client through DI.

**`ReportBuilderService` before `ReportWriterPlugin`.** The builder (T005) is a pure, stateless service that takes run data and returns a formatted markdown string. Implementing it first keeps `ReportWriterPlugin` (T006) focused solely on orchestration — load data, call builder, post to Jira, handle failure. The builder is also independently unit-testable against all report format requirements (AC-002, AC-005–AC-012).

**Plugin before worker step.** `ReportDeliveryStep` (T007) is a thin orchestrator that calls `ReportWriterPlugin` and updates run state. It cannot be written until the plugin exists. This mirrors the step/plugin split pattern used in features 0002 and 0003.

**Worker integration last.** T008 wires `ReportDeliveryStep` into `TestRunJobProcessor` after `ApiTestExecutionStep`. This is the fourth sequential edit to the processor across features 0001–0004 and carries the highest destabilisation risk. Doing it last, after the new step is fully unit-tested, protects all earlier pipeline stages.

**Delivery failure notification via Application Insights.** When Jira delivery fails, Testurio cannot post a Jira comment — the delivery channel itself is broken. For POC, the failure is logged to Application Insights (T007) and the run status is set to `ReportDeliveryFailed`, making it visible in the run history (AC-016). This satisfies AC-015 without requiring a separate notification channel.

**Integration test extends 0003's test.** T011 adds the report delivery step to the existing pipeline integration test rather than creating a new fixture. The full four-step pipeline (trigger → generate → execute → report) is exercised in one place, making regression detection straightforward as feature 0005 is added.

**Cross-feature dependencies.** This feature depends on features 0001–0003 being implemented first: `TestRun` / `ITestRunRepository` (0001) for run status updates and Jira work item ID; `TestScenario` / `ITestScenarioRepository` (0002) for scenario titles and outcomes; `StepResult` / `IStepResultRepository` (0003) for per-step detail. Feature 0005 will extend the report comment built by `ReportBuilderService` with execution log content — `ReportBuilderService` should accept an optional log section parameter to allow 0005 to append without modifying the core builder logic.

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
