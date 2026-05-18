# Implementation Plan — Executor Router — HTTP & Playwright (0029)

## Tasks

- [x] T001 [Domain] Create `ExecutionResult` record (`IReadOnlyList<ApiScenarioResult> ApiResults`, `IReadOnlyList<UiE2eScenarioResult> UiE2eResults`) — `source/Testurio.Core/Models/ExecutionResult.cs`
- [x] T002 [Domain] Create `ApiScenarioResult` record (`string ScenarioId`, `string Title`, `bool Passed`, `long DurationMs`, `IReadOnlyList<AssertionResult> AssertionResults`) and `AssertionResult` record (`string Type`, `bool Passed`, `string Expected`, `string Actual`) — `source/Testurio.Core/Models/ApiScenarioResult.cs`
- [x] T003 [Domain] Create `UiE2eScenarioResult` record (`string ScenarioId`, `string Title`, `bool Passed`, `long DurationMs`, `IReadOnlyList<StepExecutionResult> StepResults`) and `StepExecutionResult` record (`int StepIndex`, `string Action`, `bool Passed`, `string? ErrorMessage`, `string? ScreenshotBlobUri`) — `source/Testurio.Core/Models/UiE2eScenarioResult.cs`
- [x] T004 [Domain] Define `IExecutorRouter` interface (`ExecuteAsync(GeneratorResults results, ProjectConfig projectConfig, Guid userId, Guid runId, CancellationToken ct) → Task<ExecutionResult>`) — `source/Testurio.Core/Interfaces/IExecutorRouter.cs`
- [x] T005 [Domain] Define `IHttpExecutor` interface (`ExecuteAsync(IReadOnlyList<ApiTestScenario> scenarios, ProjectConfig projectConfig, CancellationToken ct) → Task<IReadOnlyList<ApiScenarioResult>>`) — `source/Testurio.Core/Interfaces/IHttpExecutor.cs`
- [x] T006 [Domain] Define `IPlaywrightExecutor` interface (`ExecuteAsync(IReadOnlyList<UiE2eTestScenario> scenarios, ProjectConfig projectConfig, Guid userId, Guid runId, CancellationToken ct) → Task<IReadOnlyList<UiE2eScenarioResult>>`) — `source/Testurio.Core/Interfaces/IPlaywrightExecutor.cs`
- [x] T007 [Domain] Define `IScreenshotStorage` interface (`UploadAsync(string userId, Guid runId, string scenarioId, int stepIndex, byte[] png, CancellationToken ct) → Task<string>`) — `source/Testurio.Core/Interfaces/IScreenshotStorage.cs`
- [x] T008 [Domain] Create `ExecutorRouterException` (`string Message`, optional inner exception) — `source/Testurio.Core/Exceptions/ExecutorRouterException.cs`
- [x] T009 [Domain] Extend `TestRun` entity with `ExecutionWarnings` (`string[]`, default empty array) — `source/Testurio.Core/Entities/TestRun.cs`
- [x] T010 [Infra] Implement `BlobScreenshotStorage` (`IScreenshotStorage`; uploads PNG bytes to `test-screenshots` container at path `{userId}/{runId}/{scenarioId}/step-{stepIndex}.png`; returns full blob URI) — `source/Testurio.Infrastructure/Storage/BlobScreenshotStorage.cs`
- [x] T011 [Infra] Register `IScreenshotStorage` as `BlobScreenshotStorage` in infrastructure DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T012 [Infra] Update `TestRunRepository` Cosmos write path to persist `executionWarnings` field — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`
- [x] T013 [App] Implement `HttpExecutor` (`IHttpExecutor`; uses `IHttpClientFactory`; iterates scenarios sequentially; evaluates all assertions per scenario; captures `DurationMs`; populates `AssertionResult.Actual` for all three assertion types; marks all assertions failed with exception message when HTTP request throws) — `source/Testurio.Pipeline.Executors/HttpExecutor.cs`
- [x] T014 [App] Implement `PlaywrightExecutor` (`IPlaywrightExecutor`; launches headless Chromium once per invocation; one browser context per scenario; executes steps in order; skips remaining steps after first failure; captures screenshot via `IScreenshotStorage` on failed assertion steps only; logs structured warning when Blob upload fails; disposes browser in `finally`) — `source/Testurio.Pipeline.Executors/PlaywrightExecutor.cs`
- [x] T015 [App] Implement `ExecutorRouter` (`IExecutorRouter`; throws `ExecutorRouterException` when both lists empty; dispatches non-empty lists in parallel via `Task.WhenAll`; merges results into `ExecutionResult`) — `source/Testurio.Pipeline.Executors/ExecutorRouter.cs`
- [x] T016 [Config] Register `IExecutorRouter`, `IHttpExecutor`, `IPlaywrightExecutor` in pipeline DI — `source/Testurio.Pipeline.Executors/DependencyInjection.cs`
- [x] T017 [Config] Add Playwright Chromium browser install step to Worker Dockerfile (`RUN pwsh playwright.ps1 install chromium --with-deps` or equivalent) — `source/Testurio.Worker/Dockerfile`
- [x] T018 [Worker] Wire executor stage into `TestRunJobProcessor`: invoke `IExecutorRouter.ExecuteAsync` with merged `GeneratorResults`; catch `ExecutorRouterException` (both lists empty → fail run); catch any other executor exception → append to `TestRun.ExecutionWarnings`; persist warnings via `TestRunRepository` before invoking stage 6 — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [x] T019 [Test] Unit tests for `HttpExecutor` — `tests/Testurio.UnitTests/Pipeline/Executors/HttpExecutorTests.cs`
- [x] T020 [Test] Unit tests for `PlaywrightExecutor` (mocked `IScreenshotStorage`; assert_visible failure → screenshot captured, URI stored; Blob upload failure → warning logged, URI null, step still failed; non-assertion step failure → no screenshot taken; `assert_url` exact and prefix-match modes) — `tests/Testurio.UnitTests/Pipeline/Executors/PlaywrightExecutorTests.cs`
- [x] T021 [Test] Unit tests for `ExecutorRouter` (both lists non-empty → `Task.WhenAll` called; only API list non-empty → only `HttpExecutor` invoked; only UI list non-empty → only `PlaywrightExecutor` invoked; both empty → `ExecutorRouterException` thrown) — `tests/Testurio.UnitTests/Pipeline/Executors/ExecutorRouterTests.cs`
- [x] T022 [Test] Integration tests for the full executor stage via `TestRunJobProcessor` (both executors succeed → `ExecutionResult` forwarded to stage 6; both lists empty → run failed with structured error; cancellation token cancelled mid-execution → both executors cancelled; `ExecutionWarnings` populated when one executor fails) — `tests/Testurio.IntegrationTests/Pipeline/ExecutorsIntegrationTests.cs`

## Rationale

**Result types before interfaces, interfaces before implementations.** `ExecutionResult` (T001), `ApiScenarioResult` / `AssertionResult` (T002), and `UiE2eScenarioResult` / `StepExecutionResult` (T003) are the data contracts that all other types in this feature reference. They must be stable before any interface can declare its return type, and before any executor can be written. Defining them first also freezes the handoff contract with feature 0030 (ReportWriter), which consumes `ExecutionResult` directly.

**Interfaces before implementations.** `IExecutorRouter` (T004), `IHttpExecutor` (T005), `IPlaywrightExecutor` (T006), and `IScreenshotStorage` (T007) are declared in `Testurio.Core` so that `Testurio.Pipeline.Executors` and `Testurio.Infrastructure` can each reference only the abstractions they need, with no circular dependencies. `ExecutorRouterException` (T008) must exist before `ExecutorRouter.cs` (T015) can throw it and before `TestRunJobProcessor` (T018) can catch it.

**`TestRun.ExecutionWarnings` in domain, not infrastructure.** The field belongs on the entity (T009) so it can be set in pipeline code without importing an Infrastructure type. `TestRunRepository` (T012) is updated after the entity is extended — the Cosmos document schema is additive (schema-less) so this is a non-breaking change to existing records.

**Infrastructure before pipeline projects.** `BlobScreenshotStorage` (T010) and its DI registration (T011) must be in place before `PlaywrightExecutor` can resolve `IScreenshotStorage` from the container. `TestRunRepository` (T012) is extended to persist `executionWarnings` so that warnings are durable before stage 6 reads the `TestRun` document.

**`HttpExecutor` before `PlaywrightExecutor`.** Both are independent, but `HttpExecutor` (T013) has no external binary dependency and can be written and verified first. `PlaywrightExecutor` (T014) requires the Playwright browser binary (handled in T017) and depends on `IScreenshotStorage` — implementing it after `HttpExecutor` avoids interleaving two complex tasks.

**`ExecutorRouter` last among implementation tasks.** T015 is a thin dispatcher that composes `IHttpExecutor` and `IPlaywrightExecutor`; it can only be written cleanly once both interfaces and their result types exist. Implementing it last avoids re-visiting the router as executor signatures stabilise.

**Dockerfile change (`[Config]`, T017) must land before any container deployment.** Playwright requires the Chromium binary and OS-level dependencies to be present in the container image. T017 must be committed before the Worker image is built for integration testing.

**Worker wiring last among production tasks.** T018 (`TestRunJobProcessor` update) is the seam between this feature and the rest of the pipeline. Implementing it last ensures all interfaces, implementations, and DI registrations are stable before the integration point is touched — consistent with the pattern established in 0027 and 0028.

**Cross-feature dependencies.** This feature depends on:
- Feature 0025 (`ParsedStory`) — not directly consumed here, but `ProjectConfig` (used by executors) was introduced then
- Feature 0026 (`TestRunJobProcessor` structure; `TestType` enum; `TestRun` entity foundation)
- Feature 0027 (`TestRun` entity extensions that executor stage builds on)
- Feature 0028 (`ApiTestScenario`, `UiE2eTestScenario`, `GeneratorResults` — the primary inputs to this stage)

Feature 0030 (ReportWriter) depends on `ExecutionResult` (T001–T003) being stable before its implementation begins.

**No UI tasks.** Executor components are pure backend pipeline concerns. No portal pages, API endpoints, or dashboard statistics fields are changed by this feature. Execution outcomes are surfaced to the user only via the PM tool report written by feature 0030.

**Tests last, per QA rules.** Unit tests (T019–T021) mock `IHttpClientFactory`, Playwright browser, and `IScreenshotStorage` to cover all acceptance-criteria paths without live external calls. Integration tests (T022) exercise the full stage through `TestRunJobProcessor` using a local HTTP test server (`WebApplicationFactory` or `WireMock.Net`) and a mocked Playwright browser.

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
