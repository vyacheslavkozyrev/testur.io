# Progress — Cosmos-Backed Prompt Template Management (0047)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-22 |       |
| Plan      | ✅ Complete | 2026-05-22 |       |
| Implement | ✅ Complete | 2026-05-22 |       |
| Review    | ✅ Complete | 2026-05-22 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

All 30 plan tasks completed across 5 commits on `feature/0047-cosmos-backed-prompt-template-management`.

Key decisions made during implementation:
- `IPromptTemplateService` placed in `Testurio.Core.Interfaces` (not Infrastructure) to avoid circular dependencies from pipeline projects
- `HybridCache` mock replaced with hand-rolled fake implementations (`TestHybridCache`, `SpyRemoveHybridCache`, `ThrowingRemoveHybridCache`) because `GetOrCreateAsync<T>` extension method is non-virtual and cannot be intercepted by Moq
- `PromptAssemblyService` reduced to 4 user-turn layers (memory, custom prompt, testing strategy, story) — `{{maxScenarios}}` substitution removed as max scenario count is now baked into the `Body` at seeding time
- T030 (E2E tests) skipped — feature is backend-only with no frontend UI surface in this sprint

---

## Review — 2026-05-22

### Warnings fixed
- `source/Testurio.Pipeline.Generators/Services/PromptAssemblyService.cs:7` — class-level doc said "five ordered context layers" but only four user-turn layers exist; item 5 in the `<remarks>` list was a note about system prompt handling, not a layer; corrected count to "four" and moved the system-prompt note outside the numbered list
- `source/Testurio.Infrastructure/Prompt/PromptTemplateService.cs:37` — self-contradictory inline comment said "We do NOT use GetOrCreateAsync here" immediately before the `GetOrCreateAsync` call; replaced with accurate explanation of error-state non-caching behaviour
- `source/Testurio.Api/DTOs/PromptTemplateDtos.cs:25` — `[MinLength(1)]` on `UpdatePromptTemplateRequest.Body` only rejects empty strings but passes whitespace-only values, creating an inconsistency with the `IsNullOrWhiteSpace` check in the endpoint handler; removed `[MinLength(1)]` so all validation is handled by the single handler check

### Status: Complete

---

## Test Results

_Populated by `/test [####]`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
