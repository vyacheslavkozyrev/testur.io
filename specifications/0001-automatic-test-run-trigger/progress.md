# Progress — Automatic Test Run Trigger (0001)

## Phase Status

| Phase     | Status      | Date       | Notes                                                |
| --------- | ----------- | ---------- | ---------------------------------------------------- |
| Specify   | ✅ Complete | 2026-04-29 | 3 stories, 11 ACs — POC scope                        |
| Plan      | ✅ Complete | 2026-04-30 | 19 tasks across Domain → Infra → API → Worker → Test |
| Implement | ✅ Complete | 2026-05-02 | 19 tasks — Domain → Infra → API → Worker → Test      |
| Review    | ✅ Complete | 2026-05-04 | Pass 1: 9B/9W/7S fixed. Pass 2: 4B/6W/3S fixed. Pass 3: 6B/6W/5S fixed. Pass 4: 2B/5W/3S fixed. Meta: 3B/5W/3S fixed. Pass 5 (2026-05-04): 1W/2W fixed |
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

## Review — 2026-05-03 (pass 3)

### Blockers fixed
- `source/Testurio.Api/Services/IJiraWebhookService.cs:7` — `ProcessAsync(string userId, string projectId, ...)` changed to `ProcessAsync(Project project, ...)` to eliminate double Cosmos read (middleware already fetched the project)
- `source/Testurio.Api/Services/JiraWebhookService.cs` — removed `IProjectRepository` dependency and `GetByIdAsync` call; all internal lookups now use the pre-resolved `Project` entity passed in
- `source/Testurio.Infrastructure/DependencyInjection.cs` — `PassthroughSecretResolver` registration removed from `AddInfrastructure()` (was unconditional, would run in production); moved to `Program.cs` and `Worker/Program.cs` behind `IsDevelopment()` guard
- `source/Testurio.Api/Program.cs` — conditional `PassthroughSecretResolver` (dev-only); inline buffering lambda replaced with `UseMiddleware<RequestBodyBufferingMiddleware>()`; `AddJwtBearer()` added; `Microsoft.AspNetCore.Authentication.JwtBearer` package added to csproj
- `source/Testurio.Worker/Program.cs` — conditional `PassthroughSecretResolver` registration (dev-only)
- `source/Testurio.Api/Middleware/RequestBodyBufferingMiddleware.cs` — extracted inline buffering lambda into a named `IMiddleware` class; `WebhookProcessResult` enum extracted to its own file (`WebhookProcessResult.cs`)

### Warnings fixed
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:40` — `CanSeek` guard added before `Body.Position = 0`; returns 401 if body stream is not seekable (e.g. buffering middleware skipped)
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:66` — `OnRunCompletedAsync` now called in the "TestRun not found" dead-letter path so the run queue is not permanently stuck
- `source/Testurio.Api/Services/JiraWebhookService.cs` — TOCTOU comment updated to note that the unique constraint must be configured in `infra/modules/cosmos.bicep` before relying on it
- `source/Testurio.Api/Clients/JiraApiClient.cs:20` — `virtual` removed from `PostCommentAsync`; interface provides the substitution seam, `virtual` is redundant and misleading
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — `ResetMocks()` method added to `ApiFactory` and called from test constructor; prevents setup accumulation across tests sharing the `IClassFixture` instance
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — `ISecretResolver` registered as `PassthroughSecretResolver` in test factory (not in dev env, so must be explicit); removed stale `GetByIdAsync` setups from `PostWebhook_ValidPayloadNoActiveRun_Returns202` and `PostWebhook_MissingDescription_Returns200AndSkips`

### Suggestions fixed
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs` — `Mock<IProjectRepository>` field and `_projectRepo.Object` removed from `CreateSut()`; all `ProcessAsync("user1", "proj1", payload)` calls updated to `ProcessAsync(MakeProject(), payload)`
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs` — all `_projectRepo.Setup(GetByIdAsync)` calls removed; project passed directly, eliminating mock setup boilerplate
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs:76,88` — `_projectRepo.VerifyNoOtherCalls()` removed from early-exit tests (repository no longer injected)
- `source/Testurio.Core/Repositories/IRunQueueRepository.cs:7` — `GetQueueAsync` signature updated with `int limit = 100` parameter
- `source/Testurio.Infrastructure/Cosmos/RunQueueRepository.cs:20` — `GetQueueAsync` updated to accept `limit` and pass it as `MaxItemCount` in `QueryRequestOptions` to cap Cosmos RU consumption

### Status: Complete

---

## Review — 2026-05-03 (pass 4)

### Blockers fixed
- `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs:37` — `GetActiveRunAsync` only checked `Status == Active`; a freshly-created `Pending` run was invisible, allowing a concurrent webhook to start a second run. Predicate extended to `Active || Pending`
- `source/Testurio.Api/Program.cs:13` — `AddJwtBearer()` called without configuration; JWT validation was silently a no-op in production. Added `AzureAdB2COptions` with `[Required]` Authority/ClientId fields, `ValidateDataAnnotations().ValidateOnStart()`, and bound them to the JWT Bearer handler. Integration test factory updated with placeholder config values

### Warnings fixed
- `source/Testurio.Api/Program.cs:19` — `JiraWebhookSignatureFilter` registered `AddScoped` but all its deps are Singleton; changed to `AddSingleton` to eliminate per-request allocation
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:40` — non-seekable body returned `401` (misleads monitoring); changed to throw `InvalidOperationException` which propagates as `500 ProblemDetails` via the registered exception handler
- `source/Testurio.Api/Services/JiraWebhookService.cs:98` — `PostCommentAsync` return value was silently discarded; captured result and logs a warning if `false` (satisfies AC-009 observability)
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:88` — `OnRunCompletedAsync` was called before `CompleteMessageAsync`; a failure in queue dispatch would cause message redelivery and double-advance of the run queue. Swapped order: complete message first, then dispatch next run
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:98` — `UpdateAsync` in the failure catch block used `args.CancellationToken` which may already be cancelled on host shutdown, leaving runs permanently stuck in `Active`; changed to `CancellationToken.None`

### Suggestions fixed
- `source/Testurio.Infrastructure/Testurio.Infrastructure.csproj` — `Newtonsoft.Json` package reference removed (not used directly; Cosmos SDK bundled it); added `<AzureCosmosDisableNewtonsoftJsonCheck>true</AzureCosmosDisableNewtonsoftJsonCheck>` to Infrastructure, Api, and Worker csproj files to suppress the transitive build-time check
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs` — two test method names corrected: `ReturnsEnqueued` → `ReturnsQueued` to match the actual `WebhookProcessResult.Queued` assertion
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — `ApiFactory` mocks exposed as typed properties (`ProjectRepoMock`, `TestRunRepoMock`, etc.) instead of being retrieved via the service container; removed `AddSingleton(mock)` registrations; tests access mocks directly via `_factory.ProjectRepoMock`

### Status: Complete

---

## Review — 2026-05-03 (meta — review of review fixes)

### Blockers fixed
- `source/Testurio.Api/Program.cs:32` — `ISecretResolver` only registered under `IsDevelopment()`; non-dev environments had no registration and would throw `InvalidOperationException` on the first webhook call. Added `else` branch registering `KeyVaultSecretResolver` (stub that throws `NotImplementedException` with a clear message). Same fix applied to `Worker/Program.cs`. New `KeyVaultSecretResolver.cs` created in Infrastructure as the production stub.
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:40` — `CanSeek` guard was `throw InvalidOperationException`; exception text surfaced in `ProblemDetails.Detail` in some host configurations. Changed to `return TypedResults.Problem(statusCode: 500)` with an `Error`-level log message, keeping client response clean.
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:70` — In the `testRun is null` dead-letter path, `OnRunCompletedAsync` was called before `DeadLetterMessageAsync`; a dispatch failure left the message re-deliverable and could double-advance the queue. Swapped order: dead-letter first, then advance queue.

### Warnings fixed
- `source/Testurio.Api/Program.cs:20` — `AddJwtBearer` lambda read `builder.Configuration[...]` directly, bypassing the validated `IOptions<AzureAdB2COptions>`. Changed to `AddOptions<JwtBearerOptions>().Configure<IOptions<AzureAdB2COptions>>(...)` so JWT config is bound from the already-validated options object.
- `source/Testurio.Api/Middleware/JiraWebhookSignatureMiddleware.cs:48` — `signatureHeader.ToString().Trim()` did not handle multi-value headers (concatenated with `, `). Changed to `signatureHeader.Any(v => IsValidSignature(body, v!.Trim(), secret))` to evaluate each value independently.
- `source/Testurio.Infrastructure/Cosmos/ProjectRepository.cs:36` — `FeedIterator` returned from `ToFeedIterator()` was not disposed when returning early on match; wrapped in `using`.
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:109` — `AbandonMessageAsync` in catch block still used `args.CancellationToken`; changed to `CancellationToken.None` for consistency with the `UpdateAsync` fix above it.
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — `ResetMocks()` is called from the test constructor and is unsafe under parallel execution. Added `[Collection("JiraWebhookSerial")]` attribute and a `CollectionDefinitions.cs` file with `DisableParallelization = true`.

### Suggestions fixed
- `source/Testurio.Infrastructure/Cosmos/RunQueueRepository.cs:29` — `MaxItemCount` controls page size, not total row count; the accumulation loop returned all rows regardless of `limit`. Added `if (results.Count >= limit) break;` after each page (matches pattern in `TestRunRepository.GetByProjectAsync`).
- `source/Testurio.Api/WebhookRouteConstants.cs` — Extracted `JiraPrefix` and `BufferingPathPrefix` as shared constants; both `RequestBodyBufferingMiddleware` and `JiraWebhookController` now reference them, eliminating the duplicated route-prefix string.
- `source/Testurio.Api/Services/JiraWebhookService.cs:94` — `ResolveAsync` was called after `CreateAsync`; a Key Vault failure left an orphaned `Skipped` TestRun with no Jira comment. Reordered: resolve secret first, then write the record.

### Status: Complete

---

## Review — 2026-05-04

### Warnings fixed
- `source/Testurio.Api/Program.cs:20` — `AddAuthentication()` called without specifying the default scheme; added `JwtBearerDefaults.AuthenticationScheme` argument to prevent ambiguous scheme resolution
- `source/Testurio.Infrastructure/ServiceBus/TestRunJobSender.cs:26` — `SessionId = message.ProjectId` set on messages but processor uses `CreateProcessor` (non-session); per-project FIFO is already enforced by `RunQueueManager` at the application layer, so `SessionId` was removed to avoid incompatibility with a non-session-enabled queue
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:53,60,71` — `DeadLetterMessageAsync` calls in the JSON parse error and null-message paths used `args.CancellationToken` which may be cancelled on host shutdown; changed to `CancellationToken.None` for consistency with the established pattern

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
