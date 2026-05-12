# User Stories — Agent Router (0026)

## Out of Scope

The following are explicitly **not** part of this feature:

- Choosing generators based on story complexity or risk — routing is based on story type classification and project config only
- Configuring test type priorities per project — project config expresses intent (`api | ui_e2e | both`); the router filters, not overrides
- Retry logic if the Claude API call for type classification fails — covered by the pipeline's general failure handling
- Post-MVP test types (`smoke`, `a11y`, `visual`, `performance`) — router resolves MVP types only (`api`, `ui_e2e`)
- Any generator execution logic — the router builds the generator list and hands off; actual generation is stage 4

---

## Stories

### US-001: Classify Story into Applicable Test Types

**As the** pipeline  
**I want to** analyze the `ParsedStory` and determine which test types (api, ui_e2e) apply to it  
**So that** only relevant generators are invoked for each story, avoiding wasted AI calls and irrelevant test output

#### Acceptance Criteria

- [ ] AC-001: The router receives the `ParsedStory` from stage 1 and the project's `test_types` config (`api | ui_e2e | both`) from the run context.
- [ ] AC-002: The router uses Claude (`claude-opus-4-7`, adaptive thinking enabled) to classify the story and return a JSON array of applicable test types from the set `["api", "ui_e2e"]`.
- [ ] AC-003: The Claude prompt includes the full `ParsedStory` fields and instructs the model to return only test types whose testing approach is meaningful for the described functionality — not all configured types blindly.
- [ ] AC-004: The resolved type list is filtered against the project's `test_types` config — a type absent from the project config is never included in the output even if Claude suggests it.
- [ ] AC-005: The router returns an `AgentRouterResult` record containing the resolved `TestType[]` array and a `classificationReason` string (Claude's brief rationale).

---

### US-002: Handle Unclassifiable Stories

**As a** QA lead  
**I want** Testurio to skip a run and notify me when a story cannot be classified into any known test type  
**So that** I am alerted to stories that are not testable with the current pipeline rather than receiving empty or misleading results

#### Acceptance Criteria

- [ ] AC-006: When the resolved test type list is empty after classification and project-config filtering, the router posts a comment to the originating ADO or Jira ticket explaining that no applicable test type could be determined for this story.
- [ ] AC-007: The comment includes the classification reason returned by Claude and a suggestion to review the story's acceptance criteria or adjust the project's test type configuration.
- [ ] AC-008: The comment is posted asynchronously — a failure to post does not cause an unhandled exception in the pipeline.
- [ ] AC-009: After posting the comment, the router marks the `TestRun` record with status `Skipped — no applicable test type` and stops pipeline execution for this run.
- [ ] AC-010: A skipped run is visible in the project's run history with status and reason, and does not count as a failure toward any pass-rate metrics.

---

### US-003: Build Generator List via Factory

**As the** pipeline  
**I want** the router to instantiate the correct generator agents from the resolved type list using a factory  
**So that** stage 4 generators are constructed in a uniform, DI-friendly way without the orchestrator needing to know concrete generator types

#### Acceptance Criteria

- [ ] AC-011: The router uses `ITestGeneratorFactory` (defined in `Testurio.Core`) to produce an `ITestGeneratorAgent` instance for each resolved test type.
- [ ] AC-012: `ITestGeneratorFactory.Create(TestType)` is the only method on the interface; concrete generator implementations (`ApiTestGeneratorAgent`, `UiE2eTestGeneratorAgent`) are registered in DI and resolved by key.
- [ ] AC-013: When the resolved list contains both `api` and `ui_e2e`, the factory produces two generator instances; both are passed to stage 4 for parallel execution.
- [ ] AC-014: The router does not execute generators itself — it returns `ITestGeneratorAgent[]` to the orchestrator, which drives stage 4.
- [ ] AC-015: If `ITestGeneratorFactory.Create` is called with an unrecognised `TestType`, it throws `ArgumentOutOfRangeException` — no silent fallback or default.

---

### US-004: Emit Routing Metadata to Run Record

**As a** QA lead  
**I want** to see which test types were selected for a run and why  
**So that** I can audit routing decisions and understand why a given generator was or was not invoked

#### Acceptance Criteria

- [ ] AC-016: After routing completes (or is skipped), the `TestRun` record in Cosmos is updated with `resolvedTestTypes` (string array) and `classificationReason` (string).
- [ ] AC-017: When routing is skipped (empty list), `resolvedTestTypes` is an empty array and `classificationReason` contains Claude's rationale.
- [ ] AC-018: The routing metadata is visible in the run detail view in the project dashboard.
- [ ] AC-019: The metadata update to Cosmos is a single upsert — it does not create a new document.
