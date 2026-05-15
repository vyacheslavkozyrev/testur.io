# User Stories — QA Lead Feedback Capture via PM Tool Comments (0031)

## Out of Scope

The following are explicitly **not** part of this feature:

- Polling-based comment detection — feedback capture fires only on incoming ADO/Jira comment-created webhook events; periodic polling is not implemented here
- Automatic passRate updates on reused pipeline-generated memory entries — that logic belongs to the MemoryWriter stage (0032)
- Soft-delete of scenario-derived memory entries based on passRate — quality loop for pipeline entries is out of scope for this feature
- Cross-project memory sharing — feedback entries are always scoped to `userId + projectId`; anonymized cross-project upsert is post-MVP (0039)
- Portal UI for browsing or deleting feedback entries — covered by post-MVP feature 0040
- Feedback capture from non-comment ticket activity (e.g. field changes, attachments)
- Support for any flag syntax other than `@testurio memorize` (case-insensitive match, flag may appear anywhere in the comment body)
- Rate limiting or deduplication of feedback comments posted by the same user in rapid succession — not in scope for MVP
- Multi-user accounts — MVP is single-user; `userId` is always the project owner

---

## Stories

### US-001: Detect `@testurio memorize` Flag in Incoming Comment Webhook

**As the** pipeline  
**I want** the `FeedbackLoop` stage to inspect incoming ADO and Jira comment-created webhook payloads for the `@testurio memorize` flag  
**So that** only comments explicitly intended as feedback are processed, and unrelated PM tool activity is ignored

#### Acceptance Criteria

- [ ] AC-001: `FeedbackLoop` receives the comment-created webhook event payload forwarded from `Testurio.Api` via Service Bus, containing: `pmTool` (`"ado"` or `"jira"`), `workItemId` (string), `commentBody` (string), `commentId` (string), and `projectId` (UUID).
- [ ] AC-002: The flag detection performs a case-insensitive substring match for `@testurio memorize` anywhere in `commentBody`. Leading and trailing whitespace in the comment body does not affect detection.
- [ ] AC-003: When the flag is absent, `FeedbackLoop` exits immediately with no further action — no embedding call, no Cosmos write, no reply comment.
- [ ] AC-004: When the flag is present, `FeedbackLoop` strips the flag text from `commentBody` (removing the exact matched substring `@testurio memorize`, case-preserving the remainder) and trims the result; the trimmed remainder is the `feedbackText` used for all subsequent steps. If `feedbackText` is empty after stripping, `FeedbackLoop` exits without writing anything and posts no confirmation.
- [ ] AC-005: `FeedbackLoop` does not filter by who posted the comment — any commenter on the ticket can trigger feedback capture.
- [ ] AC-006: The `CancellationToken` is checked before each I/O step (embedding call, Cosmos write, PM tool reply); if cancelled, the stage exits without partial writes.

---

### US-002: Resolve testType from the Most Recent Run on the Work Item

**As the** pipeline  
**I want** `FeedbackLoop` to determine which test types were active on the last pipeline run for the triggering work item  
**So that** feedback entries are correctly scoped and retrievable in future generation calls for the same test type

#### Acceptance Criteria

- [ ] AC-007: `FeedbackLoop` queries `ITestRunRepository` for the most recent `TestRun` where `workItemId` matches the incoming `workItemId` and `projectId` matches the incoming `projectId`, ordered by `createdAt` descending, limit 1.
- [ ] AC-008: The `resolvedTestTypes` field on the retrieved `TestRun` is used to determine which test types were active (e.g. `["api"]`, `["ui_e2e"]`, `["api", "ui_e2e"]`).
- [ ] AC-009: One feedback entry is created per resolved test type — if the last run resolved both `api` and `ui_e2e`, two separate `TestMemory` documents are upserted, each with its respective `testType` value and the same `feedbackText` and embedding.
- [ ] AC-010: If no prior `TestRun` exists for the work item, `FeedbackLoop` logs a structured warning (`workItemId`, `projectId`, `commentId`) and exits without writing any feedback entry or posting a confirmation reply.
- [ ] AC-011: The embedding and Cosmos upsert steps (US-003) are performed for each resolved test type independently — a failure on one test type does not skip the remaining types.

---

### US-003: Embed Feedback Text and Upsert to TestMemory

**As the** pipeline  
**I want** the `feedbackText` to be embedded and stored as a `TestMemory` document tagged `source: qalead`  
**So that** `MemoryRetrieval` can inject the QA lead's domain knowledge as few-shot context on the next generation call for this project

#### Acceptance Criteria

- [ ] AC-012: `FeedbackLoop` calls `IEmbeddingService.EmbedAsync(feedbackText, cancellationToken)` to produce a `float32[1536]` vector using `text-embedding-3-small`.
- [ ] AC-013: `FeedbackLoop` calls `ITestMemoryRepository.UpsertFeedbackAsync` with: `userId` (from project config), `projectId`, `testType` (one per call, see AC-009), `feedbackText`, `storyEmbedding` (the produced vector), `source: "qalead"`, `workItemId`, `commentId`.
- [ ] AC-014: The upsert key is `workItemId + testType` — if a `TestMemory` document already exists with the same `workItemId` and `testType` (and `source: "qalead"`), it is overwritten in full; no duplicate entries accumulate for the same work item and test type.
- [ ] AC-015: The `TestMemory` document written contains: `id` (UUID v4, newly generated on insert; preserved on update), `userId`, `projectId`, `testType`, `storyEmbedding`, `storyText` (set to `feedbackText`), `scenarioText` (set to `null`), `source` (`"qalead"`), `workItemId`, `commentId`, `isDeleted` (`false`), `createdAt` (UTC ISO 8601, set on insert; preserved on update), `updatedAt` (UTC ISO 8601, always set to current time on upsert).
- [ ] AC-016: Feedback entries with `source: "qalead"` are never subject to the passRate soft-delete quality loop — `passRate` and `runCount` fields are omitted from the document entirely.
- [ ] AC-017: If `IEmbeddingService.EmbedAsync` throws, `FeedbackLoop` logs a structured error (`workItemId`, `commentId`, exception message) and rethrows — the Service Bus message is not settled and will be retried per queue policy.
- [ ] AC-018: If `ITestMemoryRepository.UpsertFeedbackAsync` throws, `FeedbackLoop` logs a structured error and rethrows — same retry behaviour as AC-017.

---

### US-004: Post Confirmation Reply to the Ticket

**As a** QA lead  
**I want** a confirmation reply posted to the ticket comment thread after my feedback is captured  
**So that** I know the note was stored and will be used in future test generation

#### Acceptance Criteria

- [ ] AC-019: After all upserts complete successfully, `FeedbackLoop` calls `IPmToolClient.PostCommentAsync` to post a top-level comment on the originating ticket (not a reply-to-comment thread).
- [ ] AC-020: The confirmation comment body is: `✅ **Feedback captured.** Your note has been stored and will be used in future {{testType}} test generation for this ticket.` — where `{{testType}}` is a comma-separated list of all resolved test types (e.g. `api`, `ui_e2e`, or `api, ui_e2e`).
- [ ] AC-021: The confirmation is posted exactly once per feedback comment event, regardless of how many test types were resolved. A single call to `PostCommentAsync` covers all resolved test types in one comment.
- [ ] AC-022: If `IPmToolClient.PostCommentAsync` throws or returns a non-2xx status, a structured warning is logged (`workItemId`, `commentId`, PM tool type, HTTP status / exception message) and `FeedbackLoop` does **not** rethrow — the Cosmos writes are already committed and the pipeline continues normally.
- [ ] AC-023: The `CancellationToken` is forwarded to `IPmToolClient.PostCommentAsync`.

---

### US-005: Wire FeedbackLoop as a Standalone Webhook-Triggered Processor

**As the** system  
**I want** comment-created webhook events to route to `FeedbackLoop` via Service Bus independently of the main test-run pipeline  
**So that** QA lead feedback is captured without blocking or coupling to test execution

#### Acceptance Criteria

- [ ] AC-024: `Testurio.Api` publishes a `CommentWebhookEvent` message to a dedicated Service Bus topic (`testurio-comment-events`) whenever it receives an ADO or Jira comment-created webhook call, regardless of whether the comment contains the `@testurio memorize` flag — flag detection happens in the worker, not the API.
- [ ] AC-025: `Testurio.Worker` subscribes to `testurio-comment-events` via a dedicated `CommentEventJobProcessor` that deserialises the message and calls `IFeedbackLoop.ProcessAsync(commentEvent, cancellationToken)`.
- [ ] AC-026: `IFeedbackLoop` is defined in `Testurio.Core` with signature `ProcessAsync(CommentWebhookEvent evt, CancellationToken ct) → Task`.
- [ ] AC-027: `CommentWebhookEvent` is a record in `Testurio.Core` with properties: `PmTool` (string), `WorkItemId` (string), `CommentBody` (string), `CommentId` (string), `ProjectId` (Guid).
- [ ] AC-028: On unhandled exception from `IFeedbackLoop.ProcessAsync`, `CommentEventJobProcessor` does not settle the Service Bus message — it is returned to the queue for retry up to the configured dead-letter threshold.
- [ ] AC-029: The ADO comment webhook handler in `Testurio.Api` (`/webhooks/ado/comments`) authenticates the request via HMAC signature (same mechanism as the existing status-change webhook) before publishing to Service Bus.
- [ ] AC-030: The Jira comment webhook handler in `Testurio.Api` (`/webhooks/jira/comments`) authenticates via the Jira shared secret before publishing to Service Bus.
