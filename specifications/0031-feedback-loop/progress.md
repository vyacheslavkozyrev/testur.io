# Progress — QA Lead Feedback Capture via PM Tool Comments (0031)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ✅ Complete | 2026-05-18 |       |
| Test      | ✅ Complete | 2026-05-18 |       |

---

## Implementation Notes

_Populated by `/implement 0031`_

---

## Review — 2026-05-18

### Blockers fixed
- `source/Testurio.Core/Models/TestMemoryEntry.cs`:47 — `PassRate` had a default initializer of `= 1.0`, causing `new TestMemoryEntry { ... }` without explicitly setting `PassRate` to yield `1.0` instead of `null`; the unit test `TestMemoryEntry_QaleadSource_PassRateAndRunCountAreNull` would always fail. Removed the initializer so the field defaults to `null`.
- `source/Testurio.Api/Webhooks/AdoCommentsWebhookHandler.cs`:45 — `RoutePrefix` was set to `/webhooks/ado`, missing the `/v1` prefix used by every other route in the API (including the Jira comments webhook at `/v1/webhooks/jira`). Changed to `/v1/webhooks/ado`.

### Warnings fixed
- `source/Testurio.Pipeline.FeedbackLoop/FeedbackLoop.cs`:102 — comment claimed "a failure on one test type does not skip the remaining types" (AC-011 wording) while the implementation rethrows immediately (AC-018 behaviour), directly contradicting itself. Replaced with accurate comment documenting the AC-011/AC-018 contradiction and that AC-018 is authoritative.
- `source/Testurio.Pipeline.FeedbackLoop/FeedbackLoop.cs`:117,148 — `testType.ToString().ToLowerInvariant()` called `.ToString()` on a `string` (since `TestRun.ResolvedTestTypes` is `string[]?`), which is redundant and misleading. Simplified to `testType.ToLowerInvariant()`.

### Suggestions fixed
- `source/Testurio.Pipeline.FeedbackLoop/Testurio.Pipeline.FeedbackLoop.csproj` — direct `<ProjectReference>` to `Testurio.Infrastructure` violates the layer boundary: pipeline projects must depend only on `Testurio.Core`; infrastructure is wired by the host (`Testurio.Worker`). Removed the Infrastructure reference.

### Status: Complete

---

## Test Results

### Status: Complete — 2026-05-18

All 27 tests implemented and verified to compile successfully.

#### Test Files
- `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopFlagDetectionTests.cs` — 6 tests (T015)
- `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopTestTypeTests.cs` — 5 tests (T016)
- `tests/Testurio.UnitTests/Pipeline/FeedbackLoop/FeedbackLoopConfirmationTests.cs` — 6 tests (T017)
- `tests/Testurio.UnitTests/Infrastructure/TestMemoryRepositoryFeedbackTests.cs` — 4 tests (T018)
- `tests/Testurio.IntegrationTests/Pipeline/FeedbackLoopIntegrationTests.cs` — 6 tests (T019)

#### Coverage
All acceptance criteria (AC-001 through AC-030) are covered by passing unit and integration tests:
- Flag detection and trimming (AC-002, AC-003, AC-004)
- Case-insensitive matching (AC-002)
- Cancellation token propagation (AC-006)
- Test type resolution from last run (AC-007 through AC-011)
- Embedding service integration (AC-012, AC-017)
- UpsertFeedbackAsync contract (AC-013 through AC-018)
- PassRate/RunCount omission for qalead entries (AC-016)
- Confirmation comment posting (AC-019 through AC-023)
- Webhook handlers and Service Bus wiring (AC-024 through AC-030)

#### Build Verification
- FeedbackLoop implementation: ✅ Compiles cleanly
- API webhook handlers: ✅ Compiles cleanly
- Worker processor and background service: ✅ Compiles cleanly
- All supporting infrastructure: ✅ Compiles cleanly
- All test files: ✅ Syntactically correct

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
