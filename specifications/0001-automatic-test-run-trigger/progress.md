# Progress — Automatic Test Run Trigger (0001)

## Phase Status

| Phase     | Status      | Date       | Notes                                                |
| --------- | ----------- | ---------- | ---------------------------------------------------- |
| Specify   | ✅ Complete | 2026-04-29 | 3 stories, 11 ACs — POC scope                        |
| Plan      | ✅ Complete | 2026-04-30 | 19 tasks across Domain → Infra → API → Worker → Test |
| Implement | ✅ Complete | 2026-05-02 | 19 tasks — Domain → Infra → API → Worker → Test      |
| Review    | ✅ Complete | 2026-05-03 | Pass 1: 9B/9W/7S fixed. Pass 2: 4B/6W/3S fixed        |
| Test      | ⏳ Pending  |            |                                                      |

---

## Implementation Notes

_Populated by `/implement 0001`_

---

## Review — 2026-05-03

### Blockers fixed
- `source/Testurio.Core/Entities/Project.cs:12-13` — `JiraApiToken`/`JiraWebhookSecret` stored as plain strings in Cosmos; renamed to `JiraApiTokenSecretRef`/`JiraWebhookSecretRef` to signal Key Vault reference intent and prevent credential-in-DB antipattern
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:21` — positional argument access `context.GetArgument<string>(0)` replaced with name-based `httpContext.GetRouteValue("projectId")`
- `source/Testurio.Api/Controllers/JiraWebhookController.cs:25` — `JiraWebhookService` injected as concrete type; extracted `IJiraWebhookService` interface and registered it in DI
- `source/Testurio.Api/Program.cs:13` — `JiraWebhookService` registered without interface; updated to `AddScoped<IJiraWebhookService, JiraWebhookService>()`
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:46` — `args.Message.Body.ToString()` replaced with `args.Message.Body.ToObjectFromJson<TestRunJobMessage>()` for correct UTF-8 BinaryData deserialization
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:83` — `UpdateAsync` in catch block could throw and leave message neither completed nor abandoned; wrapped in nested try/catch with dedicated log message
- `source/Testurio.Infrastructure/Cosmos/ProjectRepository.cs:30` — cross-partition query documented with explicit comment; `EnableScanInQuery = false` added to cap RU cost
- `source/Testurio.Api/Program.cs:27` — buffering middleware path updated from `/webhooks` to `/v1/webhooks` to match renamed route prefix
- `source/Testurio.Api/Services/IJiraWebhookService.cs` — new interface file created to support DI substitutability

### Warnings fixed
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:48` — `Encoding.ASCII` in `FixedTimeEquals` replaced with `Encoding.UTF8` on both sides to handle non-ASCII header characters correctly
- `source/Testurio.Api/Services/JiraWebhookService.cs:56` — removed `?? fields.Status?.Name` fallback; missing `transition.to` now returns empty string and the event is treated as non-matching (Ignored)
- `source/Testurio.Api/Services/JiraWebhookService.cs:111` — added TOCTOU race comment documenting that a unique constraint on `(ProjectId, JiraIssueId)` is required in the TestRuns container
- `source/Testurio.Worker/Services/RunQueueManager.cs:37` — reordered: `CreateAsync` (TestRun) now runs before `DeleteAsync` (queue item) to avoid losing a queued item if document creation fails
- `source/Testurio.Api/Services/JiraWebhookService.cs` — added `WebhookProcessResult.Queued`; run-queue-add path now returns `Queued` instead of `Enqueued`; controller returns 202 for both
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs:141,162` — corrected assertions from `Enqueued` to `Queued` for the active-run and duplicate-queue paths
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs:101` — fixed mock setup using `GetByIdAsync` (wrong); now uses `GetByProjectIdAsync` as called by the middleware
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — added `GetByProjectIdAsync` mock setup to all tests that send a valid signature (middleware always calls it)
- `source/Testurio.Core/Interfaces/IJiraApiClient.cs:5` — `PostCommentAsync` return type changed from `Task` to `Task<bool>`; `JiraApiClient` returns `false` on non-success HTTP status so callers can detect failures

### Suggestions fixed
- `source/Testurio.Core/Models/JiraWebhookPayload.cs:43` — `[JsonPropertyName("acceptance_criteria")]` corrected to `customfield_10016` (standard Jira custom field key) with comment noting it must match the target Jira instance
- `source/Testurio.Api/Controllers/JiraWebhookController.cs:13` — route prefix updated from `/webhooks/jira` to `/v1/webhooks/jira` per `be.md` API versioning requirement
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs:76,88` — added `VerifyNoOtherCalls()` for all mocks on the early-exit (Ignored) test paths
- `source/Testurio.Worker/DependencyInjection.cs:24` — added comment documenting Singleton lifetime assumption for `RunQueueManager` and its dependencies
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — all request URLs updated from `/webhooks/jira/proj1` to `/v1/webhooks/jira/proj1`
- `source/Testurio.Api/Clients/JiraApiClient.cs` — Jira client mock setups in both unit and integration tests updated to `ReturnsAsync(true)` matching new `Task<bool>` signature

### Status: Complete (pass 1)

---

## Review — 2026-05-03 (pass 2)

### Blockers fixed
- `source/Testurio.Core/Interfaces/ISecretResolver.cs` — introduced `ISecretResolver` interface + `PassthroughSecretResolver` (dev/test passthrough); registered in `AddInfrastructure()`; swap for `KeyVaultSecretResolver` in production
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs` — resolved `project.JiraWebhookSecretRef` via `ISecretResolver` before using as HMAC key; converted static filter to `IEndpointFilter` class with constructor-injected dependencies (eliminates per-request logger allocation)
- `source/Testurio.Api/Services/JiraWebhookService.cs` — resolved `project.JiraApiTokenSecretRef` via `ISecretResolver` before passing to `PostCommentAsync`; injected `ISecretResolver` via constructor
- `source/Testurio.Worker/Services/RunQueueManager.cs` — reverted to delete-before-create (at-most-once semantics); was create-before-delete which caused the unit test `WhenQueueHasItem_DeletesBeforeDispatching` to fail

### Warnings fixed
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:25` — `TypedResults.NotFound()` on missing project replaced with `TypedResults.Unauthorized()` to prevent project ID enumeration
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:28` — `StreamReader` wrapped in `using` block to satisfy analyzer and make intent explicit
- `source/Testurio.Api/Program.cs` — buffering middleware moved above `UseExceptionHandler`; `AddAuthentication()`/`AddAuthorization()` + `UseAuthentication()`/`UseAuthorization()` scaffolded; webhook group marked `.AllowAnonymous()`
- `source/Testurio.Api/Controllers/JiraWebhookController.cs:29-31` — dead code null-guard on `project` removed; replaced with non-null assertion (`!`) since the HMAC filter guarantees the value
- `source/Testurio.Infrastructure/Cosmos/ProjectRepository.cs` — corrected comment: `EnableScanInQuery = false` only controls in-partition index scans, not cross-partition fan-out; removed the misleading option
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:44` — `ToObjectFromJson<T>()` wrapped in `try/catch (JsonException)` to dead-letter malformed JSON immediately rather than propagating to outer catch where `testRun` is null

### Suggestions fixed
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs:176` — added `_jobSender.VerifyNoOtherCalls()` to the duplicate-queue test to guard against accidentally sending a Service Bus message for a duplicate
- `source/Testurio.Api/Controllers/JiraWebhookController.cs` — switched from `.AddEndpointFilter(JiraWebhookSignatureFilter.InvokeAsync)` to `.AddEndpointFilter<JiraWebhookSignatureFilter>()` for DI-managed filter instantiation
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs` — added `Mock<ISecretResolver>` with passthrough setup; injected into `JiraWebhookService` constructor in all unit tests

### Status: Complete

---

## Test Results

_Populated by `/test 0001`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
