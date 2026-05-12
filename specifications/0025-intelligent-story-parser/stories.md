# User Stories — Intelligent Story Parser (0025)

## Out of Scope

The following are explicitly **not** part of this feature:

- Editing or amending the work item in ADO/Jira to match the Testurio template — the parser converts the story in memory only; the original ticket is never modified
- Rejecting or skipping a run because the story did not match the template — parsing always produces a result (direct or AI-converted); runs are never silently dropped due to formatting
- Configuring the Testurio story template per project — the template is fixed at the platform level
- Retry logic if the Claude API call fails during AI conversion — covered by the pipeline's general failure handling
- Storing the raw (pre-conversion) story text in Cosmos alongside the structured output
- Any parsing logic specific to UI E2E, smoke, a11y, visual, or performance test types — parsing is type-agnostic

---

## Stories

### US-001: Direct Parse of a Template-Conformant Story

**As the** pipeline  
**I want to** detect when an incoming work item already matches the Testurio story template and parse it directly into structured JSON  
**So that** no unnecessary AI call is made for well-formatted stories, keeping latency and cost low

#### Acceptance Criteria

- [ ] AC-001: The parser checks whether the work item contains all three required sections: a non-empty title, a non-empty description, and at least one acceptance criterion.
- [ ] AC-002: If all three sections are present and non-empty, the story is parsed directly into a structured `ParsedStory` JSON object without calling Claude.
- [ ] AC-003: The `ParsedStory` object contains: `title` (string), `description` (string), `acceptance_criteria` (string array), `entities` (string array, may be empty), `actions` (string array, may be empty), and `edge_cases` (string array, may be empty).
- [ ] AC-004: `entities`, `actions`, and `edge_cases` are extracted from the description and acceptance criteria text using rule-based heuristics; if none are detected, the arrays are empty rather than null.
- [ ] AC-005: The direct-parse path completes without any outbound HTTP call to the Claude API.
- [ ] AC-006: The pipeline proceeds to the next stage (AgentRouter / stage 2) immediately after a successful direct parse.

---

### US-002: AI-Assisted Conversion of a Non-Conformant Story

**As the** pipeline  
**I want to** call Claude to convert a poorly-formatted work item into a structured `ParsedStory` JSON object  
**So that** every story — regardless of how it was written — can be processed by the downstream pipeline stages

#### Acceptance Criteria

- [ ] AC-007: When the template check fails (title, description, or acceptance criteria is missing or empty), the parser calls the Anthropic Claude API with the raw work item content.
- [ ] AC-008: The Claude call uses model `claude-opus-4-7` with adaptive thinking enabled and a system prompt that instructs Claude to return only a valid JSON object matching the `ParsedStory` schema.
- [ ] AC-009: The Claude response is parsed and validated against the `ParsedStory` schema; if the response is malformed or missing required fields, the run is marked as failed with error detail "StoryParser: invalid AI response".
- [ ] AC-010: On a successful AI conversion, the pipeline proceeds to the next stage using the converted `ParsedStory` — the run is not halted.
- [ ] AC-011: The AI conversion path is used exclusively when the template check fails; it is never invoked for template-conformant stories.

---

### US-003: Warning Comment Posted to the PM Tool Ticket

**As a** QA lead  
**I want** Testurio to post a warning comment on any ADO or Jira ticket that did not match the story template  
**So that** I am informed that AI conversion was used and can bring the story into the standard format for future runs

#### Acceptance Criteria

- [ ] AC-012: When the AI conversion path is taken, the system posts a warning comment to the originating work item (ADO or Jira) immediately after the Claude call succeeds.
- [ ] AC-013: The warning comment states clearly that the story did not match the Testurio template, that AI conversion was applied, and provides a link to the documentation explaining the required template format.
- [ ] AC-014: The warning comment is posted asynchronously — a failure to post the comment does not halt the pipeline run.
- [ ] AC-015: When the direct-parse path is taken, no comment is posted to the PM tool ticket.
- [ ] AC-016: The comment is posted using the same PM tool client (ADO or Jira) that delivered the original webhook event, determined from the run context.

---

### US-004: Pipeline Continuity — No Silent Skips

**As a** QA lead  
**I want** every triggering story to result in either a completed pipeline run or an explicit failure with a reason  
**So that** I never wonder whether a story was tested and can always find its outcome in the project history

#### Acceptance Criteria

- [ ] AC-017: A story that fails the template check but is successfully converted by Claude results in a pipeline run that continues to completion — it is never silently dropped.
- [ ] AC-018: A story that fails the template check and whose Claude conversion also fails is recorded in the run history with status "Failed — StoryParser error" and the specific error detail.
- [ ] AC-019: A story that passes the template check and is parsed directly always continues to the next pipeline stage — it is never dropped due to parsing.
- [ ] AC-020: The `TestRun` record in Cosmos is updated with `parserMode` (`direct` or `ai_converted`) after the parse step completes, so the outcome is visible in run history.

---

### US-005: ParsedStory Schema Integrity

**As the** downstream pipeline stages (AgentRouter, MemoryRetrieval, Generators)  
**I want** the `ParsedStory` object produced by the parser to always conform to the defined schema  
**So that** I can deserialize and use it without defensive null-checks or per-caller schema validation

#### Acceptance Criteria

- [ ] AC-021: `ParsedStory` is defined as an immutable record in `Testurio.Core` with required non-null properties for `title`, `description`, and `acceptance_criteria`.
- [ ] AC-022: `entities`, `actions`, and `edge_cases` are always initialized as empty arrays when absent — never null.
- [ ] AC-023: The `IStoryParser` interface in `Testurio.Core` exposes a single method `ParseAsync(WorkItem workItem, CancellationToken ct)` returning `Task<ParsedStory>` and throws a typed `StoryParserException` on unrecoverable failure.
- [ ] AC-024: Both the direct-parse path and the AI-conversion path return a `ParsedStory` instance that passes schema validation before being returned to the caller.
