# Progress — Memory Retrieval Service (0027)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-12 |       |
| Plan      | ✅ Complete | 2026-05-12 |       |
| Implement | ✅ Complete | 2026-05-15 |       |
| Review    | ✅ Complete | 2026-05-15 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-15

### Blockers fixed
- `source/Testurio.Core/Models/TestMemoryEntry.cs`:9 — Missing `StoryEmbedding float[]` field; the DiskANN query references `c.storyEmbedding` but the model had no such property, silently breaking MemoryWriter writes and all future retrievals. Field added as `float[]? StoryEmbedding { get; init; }`.
- `source/Testurio.Pipeline.MemoryRetrieval/MemoryRetrievalService.cs`:19 — `MemoryRetrievalService` depended on concrete `TestMemoryRepository` (an infrastructure class), violating the plan's infrastructure-ignorance requirement and making it untestable via interface mock. Introduced `ITestMemoryRepository` in `Testurio.Core/Interfaces`, updated `TestMemoryRepository` to implement it, updated the pipeline service and DI registrations to use the interface, and removed the `Testurio.Infrastructure` project reference from `Testurio.Pipeline.MemoryRetrieval.csproj`.
- `source/Testurio.Infrastructure/Cosmos/TestMemoryRepository.cs`:43 — Cosmos DiskANN `VectorDistance ORDER BY` combined with arbitrary `WHERE` predicates (`userId`, `projectId`, `isDeleted`) is not supported and throws `BadRequestException` at runtime. Fixed by scoping only via the SDK partition key, fetching `TOP 10`, and applying `projectId` and `isDeleted` filters client-side.

### Warnings fixed
- `source/Testurio.Infrastructure/Embedding/AzureOpenAIEmbeddingService.cs`:39 — `GenerateEmbeddingAsync` called without `EmbeddingGenerationOptions`; if the deployment uses a non-default dimension the returned vector length would silently differ from the required 1536. Added `new EmbeddingGenerationOptions { Dimensions = 1536 }`.
- `source/Testurio.Pipeline.MemoryRetrieval/MemoryRetrievalService.cs`:47 — `catch (Exception)` swallowed `OperationCanceledException`, hiding pipeline cancellation during host shutdown. Added `catch (OperationCanceledException) { throw; }` before both general catch blocks.
- `source/Testurio.Core/Models/TestMemoryEntry.cs`:12 — `Id` default `Guid.NewGuid()` allocated a throwaway GUID on every Cosmos deserialization. Changed default to `string.Empty`; callers constructing new entries must set `Id` explicitly.
- `tests/Testurio.IntegrationTests/Pipeline/MemoryRetrievalIntegrationTests.cs`:19 — Integration tests used `Mock<TestMemoryRepository>` (identical structure to unit tests), providing no additional coverage. Updated to mock `ITestMemoryRepository` and added a note that a Cosmos-emulator-backed test is a remaining manual item.

### Suggestions fixed
- `source/Testurio.Core/Models/TestMemoryEntry.cs`:9 — Type was `class`; changed to `sealed class` for consistency with the rest of the domain model and to prevent unintended subclassing.
- `source/Testurio.Infrastructure/DependencyInjection.cs`:251 — `TestMemoryRepository` was registered as its concrete type. Updated to register against `ITestMemoryRepository`, matching the pattern of all other Cosmos repositories.

### Remaining issues
- `tests/Testurio.IntegrationTests/Pipeline/MemoryRetrievalIntegrationTests.cs` — Integration tests do not use the Cosmos emulator as specified in T013. All four tests are mock-only and duplicate the unit test suite. A true integration test that seeds the `TestMemory` container via `CosmosClient` pointed at the emulator and executes the real DiskANN query is required — requires manual implementation with emulator infrastructure.

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
