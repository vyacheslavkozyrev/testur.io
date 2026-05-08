# Progress — Execution Log Capture (0005)

## Phase Status

| Phase     | Status      | Date       | Notes                                                   |
| --------- | ----------- | ---------- | ------------------------------------------------------- |
| Specify   | ✅ Complete | 2026-04-30 | 4 stories, 15 ACs — POC scope                           |
| Plan      | ✅ Complete | 2026-04-30 | 14 tasks across Domain → Infra → Plugin → Worker → Test |
| Implement | ✅ Complete | 2026-05-08 | 14 tasks implemented — Domain → Infra → Plugin → Worker → Tests |
| Review    | ✅ Complete | 2026-05-08 | 10 findings (2 blockers, 5 warnings, 3 suggestions) — all fixed |
| Test      | ⏳ Pending  |            |                                                         |

---

## Implementation Notes

_Populated by `/implement 0005`_

---

## Review — 2026-05-08

### Blockers fixed
- `source/Testurio.Plugins/TestExecutorPlugin/LogPersistenceService.cs`:88 — `TruncateToThreshold` sliced UTF-8 bytes blindly at the 10 KB boundary, risking a mid-codepoint split producing malformed strings; fixed by walking back to the nearest valid codepoint start byte before decoding.
- `source/Testurio.Infrastructure/Cosmos/ExecutionLogRepository.cs`:129 — `DeleteByRunAsync` threw on any non-2xx batch response including 404, making retries non-idempotent and leaving partial-delete state unrecoverable; fixed by treating HTTP 404 batch responses as idempotent success.

### Warnings fixed
- `source/Testurio.Plugins/TestExecutorPlugin/TestExecutorPlugin.cs`:18 — two constructors (with and without `LogPersistenceService`) created a dead-code path and a risk of silently disabling log capture; collapsed to a single constructor with `LogPersistenceService? logPersistence = null` and updated Worker DI registration accordingly.
- `source/Testurio.Infrastructure/Cosmos/ExecutionLogRepository.cs`:45,70,104 — `e.ProjectId == projectId` predicate was redundant alongside the `PartitionKey` query option in all three LINQ queries; removed to avoid implying cross-partition filtering is needed; added a comment about the required composite index on `(testRunId, stepIndex)`.
- `source/Testurio.Plugins/TestExecutorPlugin/TestExecutorPlugin.cs`:162 — pipeline-level cancellation rethrow had no documentation explaining why log emission is intentionally skipped; added explanatory comment covering the AC-002 scope decision.
- `source/Testurio.Worker/Steps/ApiTestExecutionStep.cs`:62 — T009 orchestration decision (per-step persistence inside the plugin rather than per-scenario collection at the step level) was undocumented; added a comment cross-referencing T007 and AC-004.
- `source/Testurio.Infrastructure/Blob/BlobStorageClient.cs`:46 — `CreateIfNotExistsAsync` was called on every upload (one unnecessary Blob API round-trip per step), despite `BlobStorageClient` being Singleton; memoised with a `volatile bool _containerEnsured` flag.

### Suggestions fixed
- `source/Testurio.Core/Entities/ExecutionLogEntry.cs`:45 — mutable setters on response-body properties were unexplained, creating an apparent inconsistency with the init-only required fields; added a comment explaining the LogPersistenceService mutation pattern and noting a future immutable-record refactor.
- `tests/Testurio.UnitTests/Infrastructure/ExecutionLogRepositoryTests.cs`:1 — summary comment implied Cosmos integration was covered elsewhere without specifying what is not covered; replaced with an explicit statement listing the untested Cosmos behaviours (ordering, batch delete idempotency, URL pass-through).

### Status: Complete

---

## Test Results

_Populated by `/test 0005`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
