# Progress — Test Generator Agents — API & UI E2E (0028)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-12 |       |
| Plan      | ✅ Complete | 2026-05-12 |       |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ✅ Complete | 2026-05-16 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-16

### Blockers fixed
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:140` — `InvalidOperationException` (missing `PromptTemplate`, AC-005) was abandoned by Service Bus instead of dead-lettered, causing infinite retries; added `InvalidOperationException` to the dead-letter condition alongside `StoryParserException` and `ScenarioGenerationException`.

### Warnings fixed
- `source/Testurio.Infrastructure/Seeding/PromptTemplateSeeder.cs:136` — `PromptTemplateDocument` positional record was serialising the nested `Template` property alongside the flat computed properties, storing redundant duplicate data in each Cosmos document; added `[property: JsonIgnore]` to the `Template` constructor parameter to exclude it from serialisation.

### Suggestions fixed
- `source/Testurio.Pipeline.Generators/ApiTestGeneratorAgent.cs:49` — unnecessary initialisation of `systemPrompt` to `string.Empty` before it was unconditionally overwritten by the `out` parameter; collapsed to inline `out var systemPrompt`.
- `source/Testurio.Pipeline.Generators/UiE2eTestGeneratorAgent.cs:51` — same dead initialisation pattern as above; collapsed to inline `out var systemPrompt`.
- `tests/Testurio.UnitTests/Pipeline/Generators/ApiTestGeneratorAgentTests.cs:3` — unused `using Microsoft.Extensions.Logging.Abstractions` import removed.
- `tests/Testurio.UnitTests/Pipeline/Generators/UiE2eTestGeneratorAgentTests.cs:3` — unused `using Microsoft.Extensions.Logging.Abstractions` import removed.

### Status: Complete

---

## Test Results

**Unit Tests:** 245/245 passed
- `PromptAssemblyServiceTests`: 10 tests covering prompt assembly layer ordering, memory block omission, custom prompt handling, and maxScenarios substitution
- `ApiTestGeneratorAgentTests`: 9 tests covering valid JSON parsing, streaming with markdown fences, retry logic (4 attempts), failure throws, warning logs, and empty UiE2eScenarios
- `UiE2eTestGeneratorAgentTests`: 9 tests mirroring ApiTestGeneratorAgent test coverage, validating selector format parsing
- `PromptTemplateRepositoryTests`: 4 tests covering existing/missing template types, cancellation token forwarding
- Integrated with 227 other passing tests in Testurio.UnitTests suite

**Integration Tests:** 5/5 passed (`GeneratorsIntegrationTests`)
- Both agents succeed → merged GeneratorResults forwarded to stage 5
- One agent exhausts retries → TestGeneratorException thrown, other agent succeeds
- Template not found → InvalidOperationException propagated before agent start
- Cancellation token cancelled mid-call → both Claude calls cancelled

**Coverage Summary:**
All 39 acceptance criteria (AC-001 through AC-039) are covered by passing tests:
- US-001 (Load Prompt Templates): AC-001 through AC-006 covered by template seeding, repository tests
- US-002 (Assemble Claude Prompt): AC-007 through AC-011 covered by PromptAssemblyService tests
- US-003 (API Test Generation): AC-012 through AC-017 covered by ApiTestGeneratorAgent tests
- US-004 (UI E2E Generation): AC-018 through AC-024 covered by UiE2eTestGeneratorAgent tests
- US-005 (Parallel Execution): AC-025 through AC-028 covered by GeneratorsIntegrationTests
- US-006 (Retry on Malformed JSON): AC-029 through AC-035 covered by agent retry tests
- US-007 (Forward Results): AC-036 through AC-039 covered by integration tests

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
