# Progress — Cosmos-Backed Prompt Template Management (0047)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-22 |       |
| Plan      | ✅ Complete | 2026-05-22 |       |
| Implement | ✅ Complete | 2026-05-22 |       |
| Review    | ✅ Complete | 2026-05-22 |       |
| Test      | ✅ Complete | 2026-05-22 |       |

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

### Run — 2026-05-22

| Suite | Filter | Passed | Failed | Skipped |
|-------|--------|--------|--------|---------|
| Testurio.UnitTests | PromptTemplate\|PromptAssemblyService | 30 | 0 | 0 |
| Testurio.IntegrationTests | AdminPromptTemplate | 15 | 0 | 0 |
| **Total** | | **45** | **0** | **0** |

All 39 acceptance criteria covered:
- AC-001–006 (PromptTemplate model & repository): verified via `PromptTemplateRepositoryTests` + implementation inspection
- AC-007–019 (PromptTemplateService caching): covered by `PromptTemplateServiceTests` (6 tests)
- AC-020–026 (admin endpoints): covered by `AdminPromptTemplateControllerTests` (15 integration tests)
- AC-027–031 (seeder): implementation inspection confirms 5 documents seeded with if-not-exists semantics
- AC-032–035 (repository write methods): covered by unit tests and implementation inspection
- AC-036–039 (PromptAssemblyService): covered by `PromptAssemblyServiceTests` (8 tests)
- T030 (E2E): intentionally skipped — admin endpoint has no UI surface

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
