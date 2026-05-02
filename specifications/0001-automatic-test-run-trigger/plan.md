# Implementation Plan — Automatic Test Run Trigger (0001)

## Tasks

- [x] T001 [Domain] Create `TestRun` entity — `source/Testurio.Core/Entities/TestRun.cs`
- [x] T002 [Domain] Create `QueuedRun` entity — `source/Testurio.Core/Entities/QueuedRun.cs`
- [ ] T003 [Domain] Create `JiraWebhookPayload` model — `source/Testurio.Core/Models/JiraWebhookPayload.cs`
- [ ] T004 [Domain] Add `ITestRunRepository` interface — `source/Testurio.Core/Repositories/ITestRunRepository.cs`
- [ ] T005 [Domain] Add `IRunQueueRepository` interface — `source/Testurio.Core/Repositories/IRunQueueRepository.cs`
- [ ] T006 [Infra] Implement `TestRunRepository` (Cosmos DB) — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`
- [ ] T007 [Infra] Implement `RunQueueRepository` (Cosmos DB) — `source/Testurio.Infrastructure/Cosmos/RunQueueRepository.cs`
- [ ] T008 [Infra] Implement `TestRunJobSender` (Service Bus) — `source/Testurio.Infrastructure/ServiceBus/TestRunJobSender.cs`
- [ ] T009 [Infra] Register repositories and Service Bus sender in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T010 [App] Implement `JiraApiClient` (post comment to Jira issue) — `source/Testurio.Api/Clients/JiraApiClient.cs`
- [ ] T011 [App] Implement `JiraWebhookService` (validate event, check description/AC, enqueue or skip) — `source/Testurio.Api/Services/JiraWebhookService.cs`
- [ ] T012 [API] Add Jira webhook signature validation middleware — `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs`
- [ ] T013 [API] Add `JiraWebhookController` with `POST /webhooks/jira/{projectId}` — `source/Testurio.Api/Controllers/JiraWebhookController.cs`
- [ ] T014 [Worker] Implement `RunQueueManager` (FIFO processing, dedup, active run check) — `source/Testurio.Worker/Services/RunQueueManager.cs`
- [ ] T015 [Worker] Implement `TestRunJobProcessor` (Service Bus consumer, dispatches to queue manager) — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T016 [Config] Register worker services and Service Bus consumer in DI — `source/Testurio.Worker/DependencyInjection.cs`
- [ ] T017 [Test] Unit tests for `JiraWebhookService` — `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs`
- [ ] T018 [Test] Unit tests for `RunQueueManager` — `tests/Testurio.UnitTests/Services/RunQueueManagerTests.cs`
- [ ] T019 [Test] Integration tests for `POST /webhooks/jira/{projectId}` — `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs`

## Rationale

**Domain before infrastructure.** `TestRun` (T001) and `QueuedRun` (T002) are the central entities that all other layers depend on. Repository interfaces (T004, T005) are defined in `Testurio.Core` before their Cosmos DB implementations (T006, T007) so the domain layer has no dependency on infrastructure. This is the standard dependency inversion pattern used throughout the codebase.

**Infrastructure before API and Worker.** The Cosmos repositories (T006, T007) and Service Bus sender (T008) must exist before the API service (T011) and worker processor (T015) can use them. DI registration (T009) consolidates infrastructure wiring and must follow all infrastructure implementations.

**`JiraApiClient` before `JiraWebhookService`.** The client (T010) is a dependency of the service (T011) — the service calls the client to post the skip comment to Jira (AC-009). Implementing the client first keeps T011 self-contained and directly testable.

**Middleware before controller.** The signature validation middleware (T012) must be registered before the controller (T013) in the ASP.NET pipeline. Implementing it first ensures the controller is never wired up without its security guard in place.

**Worker after API.** The `RunQueueManager` (T014) and `TestRunJobProcessor` (T015) run in `Testurio.Worker` and consume messages that the API places on Service Bus. The API path (T010–T013) must be complete before the worker processing path is built, so the full message flow can be traced end-to-end during development.

**Cross-feature dependency.** `TestRun` (T001) and `ITestRunRepository` (T004) are shared by features 0002–0005. Those features must not be implemented until T001 and T004 are merged and stable. Features 0002–0005 will extend `TestRun` with additional status values and result fields — no changes to the core entity shape defined here should be made without an amendment to this plan.

**Tests last.** Unit and integration tests (T017–T019) are written after the implementation is complete so they test real behaviour rather than guiding speculative design. Tests cover the two stateful services (`JiraWebhookService`, `RunQueueManager`) and the HTTP endpoint, which together exercise all 11 acceptance criteria.

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
