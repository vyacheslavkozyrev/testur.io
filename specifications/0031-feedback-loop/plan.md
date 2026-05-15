# Implementation Plan — QA Lead Feedback Capture via PM Tool Comments (0031)

## Tasks

- [ ] T001 [Domain] Create `CommentWebhookEvent` record (`string PmTool`, `string WorkItemId`, `string CommentBody`, `string CommentId`, `Guid ProjectId`) — `source/Testurio.Core/Models/CommentWebhookEvent.cs`
- [ ] T002 [Domain] Define `IFeedbackLoop` interface (`ProcessAsync(CommentWebhookEvent evt, CancellationToken ct) → Task`) — `source/Testurio.Core/Interfaces/IFeedbackLoop.cs`
- [ ] T003 [Domain] Extend `ITestMemoryRepository` with `UpsertFeedbackAsync(string userId, Guid projectId, string testType, string feedbackText, float[] storyEmbedding, string workItemId, string commentId, CancellationToken ct) → Task` — `source/Testurio.Core/Interfaces/ITestMemoryRepository.cs`
- [ ] T004 [Domain] Extend `TestMemory` entity with fields: `Source` (string, `"pipeline"` or `"qalead"`), `WorkItemId` (string, nullable), `CommentId` (string, nullable), `UpdatedAt` (DateTimeOffset, nullable) — `source/Testurio.Core/Entities/TestMemory.cs`
- [ ] T005 [Infra] Implement `UpsertFeedbackAsync` on `TestMemoryRepository`: query by `workItemId + testType + source="qalead"` within the `userId` partition; if found, overwrite the document preserving `id` and `createdAt`; if not found, insert a new document with a generated UUID v4 `id` and `createdAt = UtcNow`; set `updatedAt = UtcNow` in all cases; omit `passRate` and `runCount` fields — `source/Testurio.Infrastructure/Cosmos/TestMemoryRepository.cs`
- [ ] T006 [Infra] Add composite index to `TestMemory` Cosmos container on `(workItemId, testType, source)` within the `userId` partition for efficient upsert lookup — `infra/modules/cosmos.bicep`
- [ ] T007 [API] Add `POST /webhooks/ado/comments` endpoint: validate HMAC signature (reuse existing ADO webhook auth middleware), deserialise ADO comment-created payload, map to `CommentWebhookEvent`, publish to `testurio-comment-events` Service Bus topic — `source/Testurio.Api/Webhooks/AdoCommentsWebhookHandler.cs`
- [ ] T008 [API] Add `POST /webhooks/jira/comments` endpoint: validate Jira shared secret (reuse existing Jira webhook auth middleware), deserialise Jira comment-created payload, map to `CommentWebhookEvent`, publish to `testurio-comment-events` Service Bus topic — `source/Testurio.Api/Webhooks/JiraCommentsWebhookHandler.cs`
- [ ] T009 [Infra] Provision `testurio-comment-events` Service Bus topic and subscription in Bicep — `infra/modules/servicebus.bicep`
- [ ] T010 [Infra] Register `testurio-comment-events` topic sender in `Testurio.Api` DI and topic receiver in `Testurio.Worker` DI — `source/Testurio.Api/DependencyInjection.cs`, `source/Testurio.Worker/DependencyInjection.cs`
- [ ] T011 [App] Implement `FeedbackLoop` (`IFeedbackLoop`):
  1. Case-insensitive substring search for `@testurio memorize` in `CommentWebhookEvent.CommentBody`; return immediately if absent.
  2. Strip flag from body and trim to produce `feedbackText`; return immediately if empty after trim.
  3. Query `ITestRunRepository` for the most recent `TestRun` by `workItemId + projectId`; log warning and return if none found.
  4. For each `testType` in `TestRun.ResolvedTestTypes`: call `IEmbeddingService.EmbedAsync(feedbackText, ct)` then `ITestMemoryRepository.UpsertFeedbackAsync(...)`.
  5. Call `IPmToolClient.PostCommentAsync` with a single confirmation comment listing all resolved test types; swallow non-fatal exceptions and log a warning.
  — `source/Testurio.Pipeline.FeedbackLoop/FeedbackLoop.cs`
- [ ] T012 [Config] Register `IFeedbackLoop` as `FeedbackLoop` in pipeline DI — `source/Testurio.Pipeline.FeedbackLoop/DependencyInjection.cs`
- [ ] T013 [Worker] Implement `CommentEventJobProcessor`: dequeue from `testurio-comment-events`, deserialise to `CommentWebhookEvent`, call `IFeedbackLoop.ProcessAsync`; on unhandled exception do not settle the message — `source/Testurio.Worker/Processors/CommentEventJobProcessor.cs`
- [ ] T014 [Worker] Register `CommentEventJobProcessor` as a hosted background service in `Testurio.Worker` — `source/Testurio.Worker/Program.cs`
- [ ] T015 [Test] Unit tests for `FeedbackLoop` flag detection (flag absent → no I/O; flag present, non-empty feedbackText → embedding + upsert called; flag present, empty feedbackText after strip → no I/O; case-insensitive match: `@Testurio Memorize`, `@TESTURIO MEMORIZE` → both matched; flag embedded mid-sentence → matched and stripped correctly) — `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopFlagDetectionTests.cs`
- [ ] T016 [Test] Unit tests for `FeedbackLoop` testType resolution (last run has `resolvedTestTypes: ["api"]` → one upsert; `["api","ui_e2e"]` → two upserts; no prior run → warning logged, no upsert, no reply; embedding throws → rethrows; upsert throws on first testType → rethrows, second testType not called) — `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopTestTypeTests.cs`
- [ ] T017 [Test] Unit tests for `FeedbackLoop` confirmation reply (all upserts succeed → `PostCommentAsync` called once with comma-separated test types; `PostCommentAsync` throws → warning logged, no rethrow; cancellation token forwarded to embedding, upsert, and PostCommentAsync calls) — `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopConfirmationTests.cs`
- [ ] T018 [Test] Unit tests for `UpsertFeedbackAsync` on `TestMemoryRepository` (no existing document → insert with new UUID and `createdAt`; existing document → overwrite preserving `id` and `createdAt`, update `updatedAt`; `passRate` and `runCount` absent from written document) — `tests/Testurio.UnitTests/Infrastructure/TestMemoryRepositoryFeedbackTests.cs`
- [ ] T019 [Test] Integration tests for the full comment event flow via `CommentEventJobProcessor` (ADO comment with flag → `TestMemory` document upserted, confirmation comment posted; Jira comment with flag → same; comment without flag → no Cosmos write, no reply; second comment on same workItem + testType → document overwritten not duplicated; no prior test run → no write, warning logged; embedding service unavailable → message not settled) — `tests/Testurio.IntegrationTests/Pipeline/FeedbackLoopIntegrationTests.cs`

## Rationale

**`CommentWebhookEvent` before everything else.** T001 is the data contract shared between `Testurio.Api` (webhook handlers), `Testurio.Worker` (job processor), and `Testurio.Pipeline.FeedbackLoop` (business logic). Until this record is stable, no other task can write code that references it. Defining it first in `Testurio.Core` keeps all three projects decoupled from one another.

**`IFeedbackLoop` before `FeedbackLoop`.** T002 defines the interface consumed by `CommentEventJobProcessor` (T013). The interface lives in `Testurio.Core` so the worker project depends only on abstractions. The concrete implementation (T011) comes after all its own dependencies are in place.

**`ITestMemoryRepository` extension before infrastructure.** T003 adds `UpsertFeedbackAsync` to the existing interface. The infrastructure implementation (T005) and the unit tests (T018) depend on the extended signature being stable. Extending the interface before writing the implementation prevents the concrete class from being written to an incomplete contract.

**`TestMemory` entity extension before infrastructure.** T004 adds `Source`, `WorkItemId`, `CommentId`, and `UpdatedAt` to the domain entity. Cosmos DB is schema-less so adding fields is non-breaking to existing documents, but the entity class must be updated before `TestMemoryRepository` (T005) can serialise the new fields.

**Infrastructure (T005, T006) before pipeline logic (T011).** `FeedbackLoop` calls `ITestMemoryRepository.UpsertFeedbackAsync` — the implementation must be resolvable from DI before the pipeline stage is wired. The Cosmos index (T006) is Bicep-only and can be applied independently, but logically belongs before production writes.

**API webhook handlers (T007, T008) before Service Bus provisioning (T009).** The handlers publish to `testurio-comment-events`; the topic must exist before the handlers are deployed. However, T007 and T008 can be written in parallel with T009 — Bicep is infrastructure-only and the handler code references the topic name as a config string. DI registration (T010) depends on both T007/T008 and T009 being complete.

**`FeedbackLoop` implementation (T011) after all domain and infra tasks.** T011 orchestrates five dependencies: `ITestRunRepository` (pre-existing from 0030), `IEmbeddingService` (pre-existing from 0027), `ITestMemoryRepository.UpsertFeedbackAsync` (T003/T005), `IPmToolClient` (pre-existing from 0025), and `CancellationToken` forwarding. Implementing it last among production code means all injected interfaces are stable and testable before the orchestration logic is written.

**`CommentEventJobProcessor` (T013) after `FeedbackLoop` (T011).** The processor is a thin dequeue-and-dispatch shim; it cannot be written before `IFeedbackLoop` is defined (T002) and registered (T012). Registration (T014 in `Program.cs`) is last among worker tasks.

**This feature does not modify `TestRunJobProcessor`.** Feedback capture is triggered by a separate comment webhook, not by the main test-run pipeline. `CommentEventJobProcessor` is an independent hosted service. There is no sequential coupling between stage 7 in the pipeline spec and this feature.

**Cross-feature dependencies.** This feature depends on:
- Feature 0025 — `IPmToolClient` for posting the confirmation comment; ADO/Jira auth middleware reused by T007/T008
- Feature 0027 — `IEmbeddingService` for `text-embedding-3-small` embedding; `ITestMemoryRepository` interface extended by T003
- Feature 0030 — `ITestRunRepository.GetMostRecentAsync(workItemId, projectId)` query, `TestRun.ResolvedTestTypes` field; `TestRun` entity must have `WorkItemId` populated (set by 0025)

Feature 0032 (MemoryWriter, Post-MVP) will also write to `TestMemory` with `source: "pipeline"`. The `Source` field added in T004 ensures both entry types coexist in the same container without collision. `MemoryRetrieval` (0027) must be updated post-MVP to return both `source` values.

**Tests last, per QA rules.** Flag-detection tests (T015) are pure string-matching — no mocking required and fastest to verify. TestType-resolution and confirmation tests (T016, T017) mock `ITestRunRepository`, `IEmbeddingService`, `ITestMemoryRepository`, and `IPmToolClient`. Infrastructure unit tests (T018) use the Cosmos emulator. Integration tests (T019) exercise the full event flow with a real Service Bus emulator, Cosmos emulator, and stubbed PM tool client.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Service Bus provisioning, DI registration — `Testurio.Infrastructure` / Bicep |
| `[App]` | Pipeline stage implementations — `Testurio.Pipeline.FeedbackLoop` |
| `[API]` | Webhook endpoints, middleware — `Testurio.Api` |
| `[Worker]` | Job processors, hosted services — `Testurio.Worker` |
| `[Config]` | DI registration, environment settings — any project |
| `[Test]` | Unit and integration test files — `tests/` |
