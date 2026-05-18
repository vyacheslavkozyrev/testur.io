# Progress — QA Lead Feedback Capture via PM Tool Comments (0031)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ✅ Complete | 2026-05-18 |       |
| Test      | ⏳ Pending  |            |       |

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

_Populated by `/test 0031`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
