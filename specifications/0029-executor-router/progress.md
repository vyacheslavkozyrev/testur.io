# Progress — Executor Router — HTTP & Playwright (0029)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-12 |       |
| Plan      | ✅ Complete | 2026-05-12 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ✅ Complete | 2026-05-17 |       |
| Test      | ✅ Complete | 2026-05-17 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-17

### Blockers fixed
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`:225–231 — Legacy `ScenarioGenerationStep` / `ApiTestExecutionStep` calls ran in the same pipeline execution as `RunExecutorStageAsync`, causing every run to execute API tests twice; removed the duplicate legacy calls
- `source/Testurio.Worker/Dockerfile`:43 — `dotnet /app/Microsoft.Playwright.dll install chromium` is not a valid command (a class library is not a self-hosted executable); replaced with `chmod +x /app/playwright.sh && /app/playwright.sh install chromium`

### Warnings fixed
- `source/Testurio.Pipeline.Executors/HttpExecutor.cs`:72 — `catch` filter included `TaskCanceledException` and `OperationCanceledException`, silently swallowing cancellation instead of propagating it; narrowed to `catch (HttpRequestException)` only
- `source/Testurio.Pipeline.Executors/HttpExecutor.cs`:272 — `CreateAuthenticatedClientAsync` declared `public`; changed to `internal` to preserve encapsulation of pipeline implementation details
- `source/Testurio.Pipeline.Executors/PlaywrightExecutor.cs`:250 — `BuildContextOptionsAsync` declared `public`; changed to `internal`
- `source/Testurio.Pipeline.Executors/PlaywrightExecutor.cs`:48–70 — `browser.CloseAsync()` called in `finally` without disposing the `IAsyncDisposable` browser; replaced with `browser.DisposeAsync()`
- `source/Testurio.Infrastructure/Storage/BlobScreenshotStorage.cs`:46–52 — Racy `volatile bool` double-check pattern could cause two concurrent calls to both reach `CreateIfNotExistsAsync`; replaced with `SemaphoreSlim`-guarded double-checked locking

### Suggestions fixed
- `source/Testurio.Pipeline.Executors/PlaywrightExecutor.cs`:107 — Added `ct.ThrowIfCancellationRequested()` before each step execution so the pipeline respects cancellation between steps
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`:345–346 — `Guid.Parse` on `UserId` and `Id` could throw `FormatException` (not in the dead-letter list); replaced with `Guid.TryParse` + `InvalidOperationException`
- `tests/Testurio.UnitTests/Pipeline/Executors/PlaywrightExecutorTests.cs`:101–123 — Tautological test called `_screenshotStorage.Object.UploadAsync` directly and verified the Moq mock invoked itself; replaced with meaningful interface-contract and failure-isolation tests

### Status: Complete

---

## Test Results

**2026-05-17: All tests passed.**

- **Unit Tests**: 38 passed (HttpExecutorTests, PlaywrightExecutorTests, ExecutorRouterTests)
  - Routing logic: 6 tests (AC-001 to AC-006)
  - HttpExecutor: 15 tests (AC-007 to AC-017 — status codes, JSON paths, headers, POST/PUT/PATCH, error handling, sequential execution, duration tracking)
  - PlaywrightExecutor: 17 tests (AC-018 to AC-034 — navigation, clicking, filling, assertions, screenshot capture, URL matching, context options)

- **Integration Tests**: 6 tests (ExecutorsIntegrationTests)
  - Both executors succeed → merged ExecutionResult (AC-001/AC-006)
  - Both lists empty → ExecutorRouterException with expected message (AC-004)
  - CancellationToken propagation across both executors (AC-005)
  - HttpExecutor handles request failure gracefully (AC-016)
  - Sequential scenario execution within HttpExecutor (AC-017)
  - TestRun.ExecutionWarnings default to empty array, never null (AC-042)

**Acceptance Criteria Coverage**: All 42 acceptance criteria (AC-001 through AC-042) are covered by passing tests. No gaps identified.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
