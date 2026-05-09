# User Stories — Custom Test Generation Prompt (0008)

## Out of Scope

The following are explicitly **not** part of this feature:

- Prompt library or saved prompt templates — no ability to store and reuse prompts across projects
- A/B testing of prompt variants — a single prompt per project only
- Account-level prompt templates — the custom prompt is project-scoped only
- Prompt version history or rollback — saving a new value overwrites the previous one with no recovery path
- Per-run prompt overrides — the same prompt applies to every test run within a project
- Global testing strategy changes — the custom prompt is additive; it never replaces or overrides the strategy field
- Any modification to the StoryParser, TestExecutor, or ReportWriter pipeline stages — only TestGenerator is affected

---

## Stories

### US-001: Enter a Custom Test Generation Prompt

**As a** QA lead
**I want to** add an optional free-text prompt to a project's core configuration
**So that** I can steer the AI toward the testing conventions, risk areas, and scope that matter most for my product

#### Acceptance Criteria

- [ ] AC-001: The custom prompt field is rendered inline within the core project configuration section, alongside the Product URL and Testing Strategy fields
- [ ] AC-002: The field is optional — a project can be saved without providing any value
- [ ] AC-003: The field accepts up to 5,000 characters; a live character counter is displayed beneath the field showing characters used versus the 5,000 limit (e.g. "342 / 5,000")
- [ ] AC-004: Submitting a value exceeding 5,000 characters triggers an inline validation error and the form is not submitted to the API
- [ ] AC-005: The API independently validates the field and returns `400 Bad Request` with a `ValidationProblemDetails` body if the value exceeds 5,000 characters when submitted directly
- [ ] AC-006: The saved value is stored on the project document in Cosmos DB under a `customPrompt` field (nullable string)
- [ ] AC-007: The field is pre-populated with the currently saved value when the user opens the project settings page

---

### US-002: Clear the Custom Prompt

**As a** QA lead
**I want to** remove the custom prompt from a project by saving an empty field
**So that** the AI falls back to base instructions without any additional guidance

#### Acceptance Criteria

- [ ] AC-008: Saving the configuration with the custom prompt field left empty (or cleared) sets `customPrompt` to `null` on the Cosmos DB document
- [ ] AC-009: After the value is cleared and saved, subsequent test generation runs use only the base system prompt and testing strategy — no custom text is appended
- [ ] AC-010: The UI shows no value in the field when the saved `customPrompt` is `null`, with the placeholder text visible

---

### US-003: Preview the Combined Prompt

**As a** QA lead
**I want to** see a read-only preview of how my custom prompt combines with the testing strategy before saving
**So that** I can understand the full context the AI will receive and catch unintended phrasing

#### Acceptance Criteria

- [ ] AC-011: A read-only preview panel is displayed adjacent to or below the custom prompt field on the project settings page
- [ ] AC-012: The preview panel shows the final composed prompt: the fixed system prompt first, followed by the testing strategy, followed by the custom prompt text appended at the end
- [ ] AC-013: The preview updates in real time as the user types in the custom prompt field — no save action is required to refresh the preview
- [ ] AC-014: The preview panel is clearly labelled (e.g. "Prompt Preview") and marked as read-only so the user understands it is not an editable field
- [ ] AC-015: When the custom prompt field is empty the preview shows only the base system prompt and testing strategy content, with a visual indicator that no custom text has been added

---

### US-004: Conflict Warning Between Custom Prompt and Testing Strategy

**As a** QA lead
**I want to** be warned when my custom prompt may conflict with the selected testing strategy
**So that** I can resolve ambiguities before they silently degrade test quality

#### Acceptance Criteria

- [ ] AC-016: When the user edits the custom prompt field the UI evaluates whether the entered text appears to contradict the project's testing strategy (client-side heuristic or server-side check — implementation detail)
- [ ] AC-017: If a potential conflict is detected a non-blocking warning message is displayed inline below the custom prompt field (e.g. "Your custom prompt may conflict with the selected testing strategy")
- [ ] AC-018: The warning does not prevent saving — the user can proceed without resolving it
- [ ] AC-019: No warning is displayed when no conflict is detected
- [ ] AC-020: The warning is re-evaluated each time the custom prompt value changes

---

### US-005: AI-Assisted Prompt Quality Check

**As a** QA lead
**I want to** request an AI review of my custom prompt before saving it
**So that** I can improve its clarity, specificity, and alignment with the testing strategy before it influences real test runs

#### Acceptance Criteria

- [ ] AC-021: A "Check Prompt" button is displayed adjacent to the custom prompt field; it is enabled only when the field contains at least one character
- [ ] AC-022: Clicking "Check Prompt" sends the current prompt text and the project's testing strategy to the API, which calls the Claude API and returns structured feedback
- [ ] AC-023: The feedback is displayed inline below the custom prompt field and includes at minimum three dimensions: Clarity, Specificity, and Potential Conflicts with the testing strategy
- [ ] AC-024: Each feedback dimension includes a short assessment and, where applicable, a concrete suggestion for improvement
- [ ] AC-025: While the API call is in progress the button shows a loading indicator and is disabled to prevent duplicate requests
- [ ] AC-026: If the API call fails (network error, Claude API error) an inline error message is displayed; the user can dismiss it and retry
- [ ] AC-027: The feedback panel is cleared automatically when the user modifies the custom prompt field after a check has been run, indicating the previous result is stale
- [ ] AC-028: The "Check Prompt" action does not save the prompt — it is purely advisory; the user must explicitly save the project configuration to persist changes
- [ ] AC-029: The API endpoint for prompt quality check is available at `POST /api/projects/{projectId}/prompt-check` and requires the authenticated user to own the project; it returns `403 Forbidden` otherwise

---

### US-006: Prompt Applied During Test Generation

**As a** QA lead
**I want to** have my custom prompt automatically used during every test generation run for the project
**So that** my guidance consistently steers AI output without any manual intervention per run

#### Acceptance Criteria

- [ ] AC-030: When the TestGenerator pipeline stage processes a job for a project that has a non-null `customPrompt`, it appends the custom prompt text after the fixed system prompt and testing strategy content in the message sent to the Claude API
- [ ] AC-031: When `customPrompt` is `null` or empty the TestGenerator sends only the base system prompt and testing strategy — no additional text is appended
- [ ] AC-032: The order of composition is always: fixed system prompt → testing strategy → custom prompt; this order cannot be changed by the user
- [ ] AC-033: The custom prompt value is read from the project document loaded from Cosmos DB at the start of the pipeline job; no separate lookup is required
- [ ] AC-034: The composed prompt does not exceed the Claude API's context limits; if appending the custom prompt would cause the total prompt to exceed the configured `MaxTokens` limit the job fails with a descriptive error written to the execution log and the run is marked as failed
