# Progress — Story-Driven Test Scenario Generation (0002)

## Phase Status

| Phase     | Status      | Date       | Notes                                                            |
| --------- | ----------- | ---------- | ---------------------------------------------------------------- |
| Specify   | ✅ Complete | 2026-04-29 | 3 stories, 14 ACs — POC scope                                    |
| Plan      | ✅ Complete | 2026-04-30 | 14 tasks across Domain → Infra → Config → Plugin → Worker → Test |
| Implement | ✅ Complete | 2026-05-07 | 14 tasks — Domain, Infra, Config, Plugin, Worker, Test           |
| Review    | ✅ Complete | 2026-05-07 | 2 blockers, 4 warnings, 2 suggestions — all fixed                |
| Test      | ⏳ Pending  |            |                                                                  |

---

## Implementation Notes

_Populated by `/implement 0002`_

---

## Review — 2026-05-07

### Blockers fixed
- `source/Testurio.Infrastructure/Cosmos/TestScenarioRepository.cs:42` — `CreateBatchAsync` used sequential `CreateItemAsync` calls instead of a `TransactionalBatch`; replaced with `TransactionalBatch.ExecuteAsync` so all scenarios are written atomically or not at all (AC-007)
- `source/Testurio.Worker/DependencyInjection.cs:80` / `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:17` — `TestRunJobProcessor` (Singleton) captured `ScenarioGenerationStep` (Transient) at construction time, freezing all Transient plugin state; replaced the captured instance with `IServiceProvider` and resolve `ScenarioGenerationStep` per-message in `ExecutePipelineAsync`

### Warnings fixed
- `source/Testurio.Infrastructure/Jira/JiraStoryClient.cs:46` — `JsonSerializerOptions` instantiated on every `GetStoryContentAsync` call; extracted to a `static readonly` field
- `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs:58` — `JsonSerializerOptions` instantiated on every `GenerateAsync` call; extracted to a `static readonly` field
- `source/Testurio.Core/Interfaces/IJiraStoryClient.cs:8` — `JiraStoryContent` data class was co-located with the interface; moved to `source/Testurio.Core/Models/JiraStoryContent.cs` following the project's interface/model separation convention
- `source/Testurio.Worker/Steps/ScenarioGenerationStep.cs:95` — `FailRunAsync` swallowed `UpdateAsync` exceptions silently, hiding AC-011/AC-013 failures; removed the try/catch so the exception propagates to the processor's outer handler

### Suggestions fixed
- `source/Testurio.Core/Entities/TestScenario.cs:7` — `TestScenario` was missing `UserId`; added `required string UserId` field for tenant-scoped queries without a join to Projects, and propagated the new parameter through `TestGeneratorPlugin.GenerateAsync` and `ScenarioGenerationStep.ExecuteAsync`
- `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs:47` — LLM response text not trimmed before JSON deserialisation; added `.Trim()` to guard against leading/trailing whitespace from the model

### Status: Complete

---

## Test Results

_Populated by `/test 0002`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
