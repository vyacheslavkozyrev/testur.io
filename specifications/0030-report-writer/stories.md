# User Stories — AI-Powered Report Writer (0030)

## Out of Scope

The following are explicitly **not** part of this feature:

- `passRate` updates on reused memory scenarios — covered by 0031 (FeedbackLoop)
- Upsert of effective scenarios to Cosmos `TestMemory` — covered by 0032 (MemoryWriter)
- Work item status transitions after report delivery — covered by 0024
- Report format and attachment settings configurable by QA lead — covered by 0009; MVP uses a fixed template
- PM tool client implementation — `IPmToolClient` is introduced in 0025 (StoryParser); this feature consumes it only
- Screenshot storage or capture — screenshots are uploaded by 0029 (ExecutorRouter); URIs are referenced in the report here
- Real-time portal status updates during pipeline execution — covered by 0043

---

## Stories

### US-001: Generate Structured Verdict Report via Claude

**As the** pipeline  
**I want** `ReportWriter` to call Claude with the `ExecutionResult` and produce a structured `ReportContent` (verdict, per-scenario summaries, recommendation)  
**So that** humans can understand test outcomes in readable prose without parsing raw assertion JSON

#### Acceptance Criteria

- [ ] AC-001: `ReportWriter` calls the Anthropic API (`claude-opus-4-7`) with `ThinkingConfigAdaptive` enabled, passing the serialised `ExecutionResult`, the `ParsedStory.title`, and `TestRun.ExecutionWarnings`.
- [ ] AC-002: The prompt instructs Claude to return a single JSON object with three top-level fields: `verdict`, `recommendation`, and `scenario_summaries`.
- [ ] AC-003: `verdict` is `"PASSED"` if and only if every `ApiScenarioResult.Passed` and every `UiE2eScenarioResult.Passed` in `ExecutionResult` is `true`; otherwise `"FAILED"`. `ReportWriter` validates this invariant against the raw `ExecutionResult` before accepting Claude's output.
- [ ] AC-004: Each entry in `scenario_summaries` contains: `scenario_id`, `title`, `passed` (bool), `duration_ms` (long), and `error_summary` (string — null when passed; for failed API scenarios, lists assertion diffs as `"Expected: <v> / Actual: <v>"`; for failed UI E2E scenarios, lists the first step error message and step index).
- [ ] AC-005: `recommendation` is exactly one of three string values:
  - `"approve"` — `verdict` is `"PASSED"` and `TestRun.ExecutionWarnings` is empty
  - `"request_fixes"` — `verdict` is `"FAILED"` and all scenario failures have clear assertion diffs or step errors (no infrastructure-level exceptions)
  - `"flag_for_manual_review"` — `verdict` is `"FAILED"` and at least one failure is due to an infrastructure-level exception, **or** `TestRun.ExecutionWarnings` is non-empty
- [ ] AC-006: Claude's JSON response is extracted by locating the first `{` and last `}` in the content text and deserialising; if deserialisation fails or required fields are missing, `ReportWriter` retries the Claude call exactly once with the same prompt; if the second attempt also fails, `ReportWriterException` is thrown.
- [ ] AC-007: The `CancellationToken` is forwarded to both the first and retry Claude API calls.
- [ ] AC-008: When all `ApiScenarios` were empty (i.e. only UI E2E ran), the `scenario_summaries` contain only UI E2E entries, and vice versa — no phantom entries are generated.

---

### US-002: Format and Post Verdict Comment to PM Tool

**As the** pipeline  
**I want** the `ReportContent` to be rendered as formatted markdown and posted as a comment to the originating ADO/Jira ticket  
**So that** the engineering team sees the test verdict in the same tool where they manage work items

#### Acceptance Criteria

- [ ] AC-009: The formatted comment begins with a single verdict line: `✅ **PASSED**` or `❌ **FAILED**`, followed by the story title in parentheses.
- [ ] AC-010: The comment body contains a per-scenario section: each scenario is listed with its result icon (`✅` or `❌`), title, and duration in milliseconds. For failed scenarios, assertion diffs or step errors from `error_summary` are rendered as an indented block below the scenario line.
- [ ] AC-011: Screenshot blob URIs from failed UI E2E steps (`StepExecutionResult.ScreenshotBlobUri`, non-null) are rendered as clickable links (`[View screenshot](<uri>)`) directly below the corresponding scenario's error block.
- [ ] AC-012: The comment ends with a `**Recommendation:** <label>` line, where label is `Approve and merge`, `Request fixes`, or `Flag for manual review`.
- [ ] AC-013: When the project's `pmTool` is `"ado"`, the comment is posted via `IPmToolClient.PostCommentAsync` which calls the ADO Work Items REST API (`/_apis/wit/workItems/{id}/comments`).
- [ ] AC-014: When the project's `pmTool` is `"jira"`, the comment is posted via `IPmToolClient.PostCommentAsync` which calls the Jira REST API (`/rest/api/3/issue/{key}/comment`).
- [ ] AC-015: On a successful post, `TestRun.PmCommentId` is set to the string ID returned by the PM tool API; it remains `null` if the post fails.
- [ ] AC-016: If `IPmToolClient.PostCommentAsync` throws or returns a non-2xx status, a structured warning is logged (`runId`, `workItemId`, PM tool type, HTTP status / exception message) and the pipeline continues — the failure does **not** throw `ReportWriterException`.

---

### US-003: Persist TestResult Record to Cosmos DB

**As the** pipeline  
**I want** a `TestResult` document written to the `TestResults` Cosmos container after report generation  
**So that** the portal dashboard and history pages can display test run data without querying the PM tool

#### Acceptance Criteria

- [ ] AC-017: `ITestResultRepository.SaveAsync` writes a `TestResult` document to the `TestResults` Cosmos container partitioned by `userId`.
- [ ] AC-018: The `TestResult` document contains: `id` (UUID v4, generated by `ReportWriter`), `userId`, `projectId`, `runId`, `storyTitle` (from `ParsedStory.title`), `verdict` (`"PASSED"` or `"FAILED"`), `recommendation` (one of three values), `totalApiScenarios` (count of `ExecutionResult.ApiResults`), `passedApiScenarios`, `totalUiE2eScenarios` (count of `ExecutionResult.UiE2eResults`), `passedUiE2eScenarios`, `totalDurationMs` (sum of all scenario `DurationMs`), `pmCommentId` (nullable string), `createdAt` (UTC ISO 8601).
- [ ] AC-019: `TestResult` is written regardless of whether the PM tool post-back succeeded or failed (AC-016); `pmCommentId` is `null` when post-back failed.
- [ ] AC-020: `TestRun.Status` is updated to `"Completed"` after `TestResult` is successfully persisted; if the Cosmos write fails, `TestRun.Status` is set to `"ReportFailed"` and `ReportWriterException` is thrown.
- [ ] AC-021: `passedApiScenarios` is the count of `ApiScenarioResult` entries with `Passed = true`; `passedUiE2eScenarios` is the count of `UiE2eScenarioResult` entries with `Passed = true`.

---

### US-004: Wire ReportWriter as Stage 6 in the Pipeline

**As the** pipeline  
**I want** `TestRunJobProcessor` to invoke `IReportWriter.WriteAsync` as stage 6, immediately after `IExecutorRouter.ExecuteAsync` returns  
**So that** every completed execution automatically produces a report and a Cosmos record without manual orchestration

#### Acceptance Criteria

- [ ] AC-022: `TestRunJobProcessor` calls `IReportWriter.WriteAsync(parsedStory, executionResult, projectConfig, testRun, cancellationToken)` as stage 6.
- [ ] AC-023: On `ReportWriterException`, `TestRunJobProcessor` sets `TestRun.Status` to `"ReportFailed"`, persists the status update via `TestRunRepository`, and rethrows — the Service Bus message is not settled and will be retried or dead-lettered per the queue policy.
- [ ] AC-024: After `WriteAsync` returns successfully, `TestRunJobProcessor` proceeds to stage 7 (FeedbackLoop / 0031) passing the same `executionResult` and `testRun` (now with `Status = "Completed"` and `PmCommentId` set).
- [ ] AC-025: `TestRun.PmCommentId` is readable by stage 7 and stage 8 from the in-memory `testRun` object — no re-read from Cosmos is required between stages.
