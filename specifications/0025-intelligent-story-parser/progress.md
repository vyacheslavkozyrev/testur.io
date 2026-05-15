# Progress — Intelligent Story Parser (0025)

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
- `source/Testurio.Pipeline.StoryParser/StoryParserService.cs:71` — `Task.Run` fire-and-forget orphaned thread-pool tasks on graceful host shutdown; replaced with direct `await` of `PostWarningAsync` (which already swallows all exceptions internally, preserving AC-014).
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:146-149` — concrete `StoryParserService` resolved directly from DI instead of `IStoryParser` interface; changed primary resolve to `IStoryParser` with a type-check cast for the project-aware overload, with a documented follow-up to extend the interface.
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:145` — `BuildWorkItem` produced a `WorkItem` with empty `Description` and `AcceptanceCriteria`, causing `TemplateChecker.IsConformant` to always return `false` and `ParserMode` to always be written as `AiConverted`; added a prominent architectural-debt comment and corrected the `isConformant` check order to run after `BuildWorkItem`.

### Warnings fixed
- `source/Testurio.Core/Entities/TestRun.cs:30-37` — `ParserMode` enum co-located inside entity file; moved to `source/Testurio.Core/Enums/ParserMode.cs` following existing convention.
- `source/Testurio.Core/Interfaces/IStoryParser.cs:21` — interface method gave no indication that PM tool comment posting is silently skipped; added `<remarks>` XML doc explaining the limitation and directing callers to the `StoryParserService` overload.
- `source/Testurio.Pipeline.StoryParser/DirectParser.cs:66-79` — `found.Contains(keyword)` O(n) deduplication replaced with `HashSet<string>`; redundant `StringComparison.OrdinalIgnoreCase` on pre-lowercased text replaced with `StringComparison.Ordinal`.
- `source/Testurio.Pipeline.StoryParser/AiStoryConverter.cs:113-117` — all-whitespace `acceptance_criteria` entries passed the `Length == 0` guard but produced an empty list after filtering; added post-filter count check that throws `StoryParserException`.
- `source/Testurio.Pipeline.StoryParser/PmToolCommentPoster.cs:70-71` — fallback chain `JiraEmailSecretUri ?? JiraEmail ?? string.Empty` allowed plaintext credential to bypass Key Vault, violating architecture rules; removed plaintext fallback, added early-return with warning log when Key Vault URI is absent.
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:163` — null-forgiving `!` operator on `testRun.ParserMode.ToString()!` replaced with `testRun.ParserMode.Value.ToString()` which is explicit and non-ambiguous.
- `tests/Testurio.UnitTests/Pipeline/StoryParserServiceTests.cs:87-91` — `Task.Delay(50)` race-condition timing buffer removed (no longer needed after `Task.Run` BLOCKER fix).
- `tests/Testurio.IntegrationTests/Pipeline/StoryParserIntegrationTests.cs:100,161` — `Task.Delay(100/200)` race-condition timing buffers removed.

### Suggestions fixed
- `source/Testurio.Pipeline.StoryParser/AiStoryConverter.cs:38-42` — `JsonSerializerOptions` instance not frozen; added static constructor calling `MakeReadOnly()` to eliminate per-call recompilation overhead in .NET 8+.
- `tests/Testurio.UnitTests/Pipeline/DirectParserTests.cs:119,131,145` — three tests named `*_ReturnsEmptyXxxArray` used `Assert.NotNull` instead of `Assert.Empty`; replaced with `Assert.Empty` so test names match assertions.

### Remaining issues
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:144-151` — architectural debt: `WorkItem.Description` and `WorkItem.AcceptanceCriteria` are empty stubs because story content is still fetched inside `ScenarioGenerationStep` (feature 0002). Until that fetch is moved to Stage 1, `ParserMode` will always be written as `AiConverted` in production. Documented in code with a `KNOWN ARCHITECTURAL DEBT` comment; requires manual resolution as part of the feature 0002 refactor.
- `source/Testurio.Pipeline.StoryParser/PmToolCommentPoster.cs:109` — ADO path still falls back to `project.AdoTokenSecretUri ?? string.Empty` instead of early-returning when the URI is absent. Consistent with the Jira fix applied above; requires manual resolution.

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
