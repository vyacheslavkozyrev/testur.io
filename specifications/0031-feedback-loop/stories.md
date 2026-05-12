# User Stories — Test Result Feedback Loop (0031)

## Out of Scope

The following are explicitly **not** part of this feature:

- Upserting new memory entries for passing runs — covered by 0032 (MemoryWriter)
- Filtering soft-deleted entries from the Cosmos vector search — enforced by the `isDeleted: false` filter already defined in 0027 (MemoryRetrieval); this feature only writes the flag
- Cross-project entry scoping rules — `passRate` updates apply equally to project-scoped and cross-project entries (`projectId: null`); scoping rules are defined in 0027 and 0032
- Manual memory-entry management by the QA lead — post-MVP, feature 0040

---

## Stories

### US-001: No-Op When No Reused Entries or No Executor Results

**As the** pipeline  
**I want** the FeedbackLoop to exit immediately when there is nothing to update  
**So that** runs with no memory history (cold start) and skipped runs are not degraded by spurious updates

#### Acceptance Criteria

- [ ] AC-001: `IFeedbackLoop.RunAsync` returns immediately without any Cosmos read or write when `memoryRetrievalResult.Scenarios` is empty.
- [ ] AC-002: `IFeedbackLoop.RunAsync` returns immediately without any Cosmos read or write when `executionResult` contains zero `ApiResults` and zero `UiE2eResults`.

---

### US-002: Determine Overall Run Outcome

**As the** pipeline  
**I want** the FeedbackLoop to compute a single pass/fail verdict from the executor results  
**So that** every reused memory entry receives the same signal from one run, reflecting the aggregate quality of the work item under test

#### Acceptance Criteria

- [ ] AC-003: The overall run outcome is `Passed` if and only if every `ApiScenarioResult.Passed` and every `UiE2eScenarioResult.Passed` in `executionResult` is `true`. A run where at least one scenario failed is `Failed`.
- [ ] AC-004: A run that contains only API results (UI E2E list is empty) derives its outcome solely from the API results, and vice versa.

---

### US-003: Update passRate on Reused Memory Entries Using EMA

**As the** pipeline  
**I want** the FeedbackLoop to update the `passRate` of each reused `TestMemoryEntry` using an exponential moving average  
**So that** recent outcomes have a stronger influence on quality signal than older runs, and gradual degradation in a scenario's reliability is reflected smoothly

#### Acceptance Criteria

- [ ] AC-005: For each `TestMemoryEntry` in `memoryRetrievalResult.Scenarios`, `FeedbackLoop` loads the entry from the `TestMemory` Cosmos container by `id` and `userId` partition key.
- [ ] AC-006: If the loaded entry has `isDeleted = true`, it is skipped — no update is applied and no warning is emitted.
- [ ] AC-007: The new `passRate` is computed as `passRate = passRate * α + result * (1 - α)`, where `result = 1.0` when the overall run outcome is `Passed` and `result = 0.0` when `Failed`, and `α` is read from configuration key `FeedbackLoop:Alpha` (default `0.8`; clamped to the inclusive range `[0.1, 0.99]` at startup).
- [ ] AC-008: `runCount` is incremented by `1` only when the overall run outcome is `Passed`. On a `Failed` outcome, `runCount` is left unchanged.
- [ ] AC-009: `lastUsedAt` is set to UTC now on every update, regardless of pass or fail.
- [ ] AC-010: Reused entries are updated sequentially (one Cosmos write per entry, not in parallel) to avoid conflicting writes if the same entry appears in both the API and UI E2E retrieval lists.

---

### US-004: Soft-Delete Degraded Memory Entries

**As the** pipeline  
**I want** entries whose quality has consistently degraded below a threshold to be soft-deleted automatically  
**So that** the MemoryRetrieval stage stops injecting low-quality few-shot examples that harm generation output

#### Acceptance Criteria

- [ ] AC-011: Before persisting the updated entry, if the newly computed `passRate < 0.5` **and** `runCount >= 5`, `isDeleted` is set to `true` in the same write operation.
- [ ] AC-012: Soft-deleted entries remain physically present in Cosmos — only `isDeleted` is set to `true`. No document is deleted.
- [ ] AC-013: When an entry is soft-deleted, a structured log entry is written at `Information` level containing `entryId`, `userId`, `passRate` (rounded to 4 decimal places), and `runCount`.
- [ ] AC-014: Entries with `runCount < 5` are never soft-deleted regardless of `passRate`, preserving entries that have not accumulated enough signal to judge reliably.

---

### US-005: Non-Throwing Stage 7 Integration

**As the** pipeline  
**I want** the FeedbackLoop to never throw or cause the pipeline to retry  
**So that** a transient Cosmos failure during passRate updates does not prevent report delivery or memory writing from completing

#### Acceptance Criteria

- [ ] AC-015: `TestRunJobProcessor` calls `IFeedbackLoop.RunAsync(executionResult, memoryRetrievalResult, testRun, cancellationToken)` as stage 7, immediately after `IReportWriter.WriteAsync` (stage 6) returns successfully.
- [ ] AC-016: Any exception thrown inside `IFeedbackLoop.RunAsync` is caught by the implementation, logged as a structured warning at `Warning` level (including `runId` and the exception message), and not rethrown. The Service Bus message is not affected.
- [ ] AC-017: After stage 7 completes (with or without an internal exception), `TestRunJobProcessor` proceeds to stage 8 (MemoryWriter / 0032) unconditionally.
- [ ] AC-018: The `CancellationToken` is forwarded to every Cosmos write call inside `RunAsync` so that pipeline cancellation (e.g. shutdown) is respected.
