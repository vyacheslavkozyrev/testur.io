# User Stories — Test Generator Agents — API & UI E2E (0028)

## Out of Scope

The following are explicitly **not** part of this feature:

- HTTP request execution and response validation — covered by 0029 (ExecutorRouter / HttpExecutor)
- Browser automation and screenshot capture — covered by 0029 (ExecutorRouter / PlaywrightExecutor)
- Project authentication config injection (Bearer token, API key, Basic Auth) into scenarios — injected by the executor at run time, not by the generator
- Per-request timeout injection — executor responsibility (0029)
- Memory writing after a run — covered by 0032 (MemoryWriter)
- `passRate` updates on reused scenarios — covered by 0031 (FeedbackLoop)
- Prompt template management UI — not in MVP scope
- Per-project prompt template overrides — templates are global; per-project steering is handled by the existing `customPrompt` field (feature 0008)
- Post-MVP test types (smoke, a11y, visual, performance) — only `api` and `ui_e2e` are generated here

---

## Stories

### US-001: Load Prompt Templates from Cosmos

**As the** pipeline  
**I want to** load the generator prompt template for each enabled test type from the `PromptTemplates` Cosmos container before invoking any generator agent  
**So that** prompt content, generator instructions, and scenario count limits can be updated without a code deployment

#### Acceptance Criteria

- [ ] AC-001: A `PromptTemplate` document exists in the `PromptTemplates` Cosmos container for each of `templateType: "api_test_generator"` and `templateType: "ui_e2e_test_generator"`.
- [ ] AC-002: Each `PromptTemplate` document contains: `id` (same as `templateType`), `templateType` (string), `version` (string), `systemPrompt` (string), `generatorInstruction` (string), and `maxScenarios` (integer).
- [ ] AC-003: The Worker orchestrator (`TestRunJobProcessor`) loads the templates for all enabled test types once per run via `IPromptTemplateRepository`, before constructing `GeneratorContext` instances.
- [ ] AC-004: The loaded `PromptTemplate` is embedded in the `GeneratorContext` passed to the corresponding agent — `ApiTestGeneratorAgent` receives the `api_test_generator` template; `UiE2eTestGeneratorAgent` receives the `ui_e2e_test_generator` template.
- [ ] AC-005: If a required template cannot be found in Cosmos, the run fails immediately with a structured error log identifying the missing `templateType`; no generator agent is invoked.
- [ ] AC-006: The initial seeded templates set `maxScenarios: 10` for `api_test_generator` and `maxScenarios: 5` for `ui_e2e_test_generator`.

---

### US-002: Assemble Claude Prompt from Context Layers

**As the** pipeline  
**I want** each generator agent to assemble its Claude prompt from a fixed, ordered set of context layers  
**So that** the model receives all relevant information in a consistent, predictable structure that produces well-grounded test scenarios

#### Acceptance Criteria

- [ ] AC-007: `PromptAssemblyService` assembles the prompt in this exact order:
  1. System prompt — from `PromptTemplate.systemPrompt`
  2. Few-shot memory examples — from `MemoryRetrievalResult.Scenarios` (formatted as numbered example blocks)
  3. Project custom prompt — from `ProjectConfig.customPrompt` (if non-null and non-empty)
  4. Testing strategy — from `ProjectConfig.testingStrategy`
  5. Parsed story — full text of title, description, acceptance criteria, entities, actions, and edge cases from `ParsedStory`
  6. Generator instruction — from `PromptTemplate.generatorInstruction`, with `{{maxScenarios}}` substituted with the integer value from the template
- [ ] AC-008: If `MemoryRetrievalResult.Scenarios` is empty, the memory examples block is omitted entirely — no placeholder section or "No examples available" text is inserted.
- [ ] AC-009: If `ProjectConfig.customPrompt` is null or empty, the custom prompt block is omitted entirely.
- [ ] AC-010: Each memory example is rendered as: `Example {{n}}: Story: <storyText> / Scenarios: <scenarioText> / Pass rate: <passRate>`.
- [ ] AC-011: The assembled prompt is never written to a log, persisted to Cosmos, or included in any error response — only the parsed output is stored.

---

### US-003: API Test Scenario Generation

**As the** pipeline  
**I want** `ApiTestGeneratorAgent` to produce a typed, assertion-complete list of API test scenarios for the given story  
**So that** the executor can validate real HTTP responses against machine-readable assertions without further interpretation

#### Acceptance Criteria

- [ ] AC-012: `ApiTestGeneratorAgent` calls Claude (`claude-opus-4-7`, adaptive thinking enabled, streaming) and parses the streamed response into `IReadOnlyList<ApiTestScenario>`.
- [ ] AC-013: Each `ApiTestScenario` contains: `id` (UUID v4), `title` (string), `method` (`GET` | `POST` | `PUT` | `PATCH` | `DELETE`), `path` (string — path and query only, no origin), `headers` (nullable string dictionary), `body` (nullable JSON object), and `assertions` (non-empty list of `Assertion`).
- [ ] AC-014: Each `Assertion` carries a `type` discriminator and type-specific fields:
  - `status_code` — `expected` (int)
  - `json_path` — `path` (JSONPath expression string) and `expected` (string value or `*` for existence-only check)
  - `header` — `name` (header name string) and `expected` (string value)
- [ ] AC-015: The number of generated scenarios does not exceed `PromptTemplate.maxScenarios`; the generator instruction explicitly states this limit.
- [ ] AC-016: Every scenario must contain at least one `status_code` assertion; the generator instruction enforces this requirement.
- [ ] AC-017: The agent returns a `GeneratorResults` record with `ApiScenarios` populated and `UiE2eScenarios` as an empty list.

---

### US-004: UI E2E Test Scenario Generation

**As the** pipeline  
**I want** `UiE2eTestGeneratorAgent` to produce a typed, step-complete list of UI end-to-end test scenarios for the given story  
**So that** the executor can drive a real browser through the described user flows without interpretation

#### Acceptance Criteria

- [ ] AC-018: `UiE2eTestGeneratorAgent` calls Claude (`claude-opus-4-7`, adaptive thinking enabled, streaming) and parses the streamed response into `IReadOnlyList<UiE2eTestScenario>`.
- [ ] AC-019: Each `UiE2eTestScenario` contains: `id` (UUID v4), `title` (string), and `steps` (ordered, non-empty list of `UiStep`).
- [ ] AC-020: Each `UiStep` carries an `action` discriminator and action-specific fields:
  - `navigate` — `url` (string)
  - `click` — `selector` (string)
  - `fill` — `selector` (string), `value` (string)
  - `assert_visible` — `selector` (string)
  - `assert_text` — `selector` (string), `expected` (string)
  - `assert_url` — `expected` (string, exact or prefix match token)
- [ ] AC-021: The generator instruction enforces the following selector preference order: (1) Playwright role/text/label locators (e.g. `role=button[name="Submit"]`), (2) `data-testid` attributes, (3) CSS selectors as last resort. Each step's selector must use the highest-priority form that can be inferred from the story.
- [ ] AC-022: Every scenario must end with at least one assertion step (`assert_visible`, `assert_text`, or `assert_url`); the generator instruction enforces this.
- [ ] AC-023: The number of generated scenarios does not exceed `PromptTemplate.maxScenarios`; the generator instruction explicitly states this limit.
- [ ] AC-024: The agent returns a `GeneratorResults` record with `UiE2eScenarios` populated and `ApiScenarios` as an empty list.

---

### US-005: Parallel Execution by Worker Orchestrator

**As the** pipeline  
**I want** both generator agents to run concurrently  
**So that** total generation time is bounded by the slower agent, not the sum of both

#### Acceptance Criteria

- [ ] AC-025: `TestRunJobProcessor` launches both enabled generator agents with `Task.WhenAll` and awaits both before passing results to stage 5 (ExecutorRouter).
- [ ] AC-026: If only one test type is enabled for the project (as resolved by stage 2 AgentRouter), only the corresponding agent is invoked — the other is not started and its scenario list in `GeneratorResults` remains empty.
- [ ] AC-027: The `CancellationToken` is forwarded to both Claude streaming calls — cancelling the run token causes both in-flight calls to be cancelled.
- [ ] AC-028: The combined `GeneratorResults` passed to stage 5 merges the `ApiScenarios` list from `ApiTestGeneratorAgent` and the `UiE2eScenarios` list from `UiE2eTestGeneratorAgent` into a single record.

---

### US-006: Retry on Malformed Claude Output

**As the** pipeline  
**I want** each generator agent to retry when Claude returns unparseable JSON  
**So that** transient model output issues do not cause a run failure without first attempting self-correction

#### Acceptance Criteria

- [ ] AC-029: On JSON parse failure (any `JsonException`), the agent retries the full Claude call up to 3 additional times (4 total attempts maximum).
- [ ] AC-030: Each retry appends the previous raw response and a correction instruction ("The previous response was not valid JSON. Return only a valid JSON array matching the required schema.") to the message history before the next call.
- [ ] AC-031: A structured warning log is emitted on each retry attempt, including run ID, test type, attempt number (e.g. `2/4`), and the `JsonException` message.
- [ ] AC-032: If all 4 attempts produce invalid JSON, the agent throws `TestGeneratorException` carrying the test type, last attempt number, last raw response (truncated to 2000 characters), and the originating `JsonException`.
- [ ] AC-033: When one agent throws `TestGeneratorException`, the other agent's `Task` is not cancelled — both tasks are independent entries in `Task.WhenAll`.
- [ ] AC-034: `TestRunJobProcessor` catches `TestGeneratorException` per agent, appends a warning string (e.g. `"api_test_generator: JSON parse failed after 4 attempts"`) to `TestRun.GenerationWarnings`, and continues to stage 5 with an empty list for the failed type.
- [ ] AC-035: If both agents throw, stage 5 still receives a `GeneratorResults` with two empty lists; stage 5 (ExecutorRouter) is responsible for determining whether to continue or fail the run.

---

### US-007: Forward Generator Results to Executor Stage

**As the** pipeline  
**I want** generator output to be attached to the pipeline context as a strongly typed record  
**So that** stage 5 (ExecutorRouter) can route each scenario list to the correct executor without inspecting raw strings

#### Acceptance Criteria

- [ ] AC-036: `GeneratorResults` is a C# record defined in `Testurio.Core` with two properties: `IReadOnlyList<ApiTestScenario> ApiScenarios` and `IReadOnlyList<UiE2eTestScenario> UiE2eScenarios`.
- [ ] AC-037: `GeneratorResults` contains no formatting or execution logic — it is a plain data container.
- [ ] AC-038: After `Task.WhenAll` resolves, `TestRunJobProcessor` attaches the merged `GeneratorResults` to the run context and writes any accumulated `GenerationWarnings` to the `TestRun` Cosmos document before invoking stage 5.
- [ ] AC-039: The `TestRun` record's `GenerationWarnings` field is an empty array when both agents complete successfully — it is never null.
