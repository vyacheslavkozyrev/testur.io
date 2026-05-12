# Progress — Custom Test Generation Prompt (0008)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-09 | 20 tasks across Domain → Infra → App → API → Worker → UI → Test |
| Implement | ✅ Complete | 2026-05-11 | 19 tasks completed (T020 E2E deferred per implement-phase rules) |
| Review    | ✅ Complete | 2026-05-11 |       |
| Test      | ✅ Complete | 2026-05-11 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-11

### Warnings fixed
- `source/Testurio.Web/src/views/ProjectSettingsPage/ProjectSettingsPage.tsx:35-38` — setState called during render to sync `customPrompt`; replaced with `useEffect` keyed on `project.projectId` to avoid the React anti-pattern
- `source/Testurio.Web/src/components/CustomPromptField/CustomPromptField.tsx:46` — dual-state pattern: `feedbackStale` intermediate flag and `useEffect` were redundant; feedback is now cleared directly in `handleChange` when feedback is non-null
- `source/Testurio.Web/src/components/CustomPromptField/CustomPromptField.test.tsx:6` — `createTheme()` called at module level in violation of `ui.md` rule; replaced with import of shared `theme` from `@/theme/theme`

### Suggestions fixed
- `source/Testurio.Web/src/components/CustomPromptField/CustomPromptField.tsx:768` — `inputProps={{ maxLength: MAX_PROMPT_LENGTH + 1 }}` allowed 5001 characters in the textarea before validation fired; corrected to `MAX_PROMPT_LENGTH` (5000)
- `source/Testurio.Web/src/views/ProjectSettingsPage/ProjectSettingsPage.tsx:47-49` — `handleCustomPromptChange` was a no-op wrapper around `setCustomPrompt`; removed and `setCustomPrompt` passed directly as `onChange` prop
- `source/Testurio.Web/src/components/CustomPromptField/CustomPromptField.test.tsx:1` — unused `fireEvent` and `waitFor` imports removed

### Status: Complete

---

## Test Results

### 2026-05-11

**Backend unit tests** — 67 passed, 0 failed
- `PromptCheckServiceTests`: 4 tests covering structured feedback, markdown fence stripping, invalid JSON, and strategy/prompt injection into LLM message
- `TestGeneratorPluginPromptCompositionTests`: 7 tests covering base prompt inclusion, strategy/custom prompt appending, null/whitespace handling, composition order, and context-limit guard

**Integration tests** — 31 passed, 0 failed
- `ProjectPromptCheckControllerTests`: 6 tests covering 200 success, 404 not found, 403 ownership, 400 empty/over-limit, and 401 unauthenticated

**Frontend component tests** — 18 passed, 0 failed
- `CustomPromptField.test.tsx`: 18 tests covering rendering, character counter, over-limit validation, conflict warning, prompt preview panel, Check Prompt button states, error display, and onChange propagation

**Bugs fixed during test phase:**
1. `TestGeneratorPlugin.ComposeSystemPrompt` was `internal static` — changed to `public static` to allow unit test access from the test assembly
2. `ProjectPromptCheckControllerTests` was missing `using Microsoft.AspNetCore.Mvc.Testing;` — added the missing import
3. `PromptCheckService` registered as `IPromptCheckService` in `Program.cs` but `ILlmGenerationClient` was not registered in the API's DI container (only in the Worker) — added `AddHttpClient<ILlmGenerationClient, AnthropicGenerationClient>` registration in `Program.cs`
4. `CustomPromptField.test.tsx` line 161 — `getByText(/Focus on auth flows./)` threw "multiple elements found" because the text appears in both the textarea and the preview panel — changed to `getAllByText(...)[0]`

**Gaps (no test coverage — deferred to E2E, T020):**
- AC-006/007: `customPrompt` persisted in Cosmos DB and pre-populated on page load
- AC-008/009/010: Clearing the prompt sets `null` in Cosmos and subsequent runs use only base prompt
- AC-033: Custom prompt read from project document at pipeline start (not a separate lookup)

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
