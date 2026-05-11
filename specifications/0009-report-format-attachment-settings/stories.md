# User Stories — Report Format & Attachment Settings (0009)

## Out of Scope

The following are explicitly **not** part of this feature:

- Per-run report format overrides — one template per project applies to every run
- Report template versioning or history — saving a new template overwrites the previous one with no recovery path
- Account-level or global default templates shared across projects — the template is project-scoped only
- Template syntax beyond placeholder token substitution — no scripting, loops, or conditional blocks in templates
- Email delivery of reports — reports are posted to the PM tool and stored in Testurio only
- Masking of sensitive values (auth tokens, PII) within log content included in the report — the QA lead is responsible for what the logs contain
- Screenshot capture itself — that is owned by feature 0018; this feature only controls whether captured screenshots are attached to the report
- Report purge or retention policy — report artifacts are retained for the lifetime of the run record (governed by feature 0005)

---

## Stories

### US-001: Upload a Report Template for a Project

**As a** QA lead  
**I want to** upload a Markdown file as the report template for a project  
**So that** every test run report follows my team's existing documentation standard without manual reformatting

#### Acceptance Criteria

- [ ] AC-001: The project settings page contains a dedicated "Report Template" section with a file upload control that accepts `.md` files only.
- [ ] AC-002: Attempting to upload a file with any extension other than `.md` triggers an inline validation error ("Only Markdown (.md) files are accepted") and the file is rejected without uploading.
- [ ] AC-003: The accepted file size limit is 100 KB; files exceeding this limit are rejected with an inline error ("Template file must be 100 KB or smaller") and not uploaded.
- [ ] AC-004: On successful upload the file is stored in Azure Blob Storage and its blob URI is persisted on the project document in Cosmos DB under `reportTemplateUri` (nullable string).
- [ ] AC-005: The API validates the uploaded content server-side and returns `400 Bad Request` with a `ValidationProblemDetails` body if the file is not valid UTF-8 text or exceeds 100 KB when submitted directly.
- [ ] AC-006: After a template is saved the settings page shows the filename of the currently uploaded template and a "Remove" action in place of the upload control.
- [ ] AC-007: When no template has been uploaded the settings page shows the upload control and a note stating "No custom template — the built-in default will be used".

---

### US-002: Remove a Report Template

**As a** QA lead  
**I want to** remove a previously uploaded report template from a project  
**So that** the project falls back to the built-in default report format without requiring me to upload a replacement

#### Acceptance Criteria

- [ ] AC-008: Clicking "Remove" on an uploaded template shows a confirmation prompt before deleting.
- [ ] AC-009: On confirmation the blob is deleted from Azure Blob Storage and `reportTemplateUri` is set to `null` on the project document.
- [ ] AC-010: After removal the settings page reverts to showing the upload control and the "No custom template" notice.
- [ ] AC-011: If the blob deletion fails the `reportTemplateUri` field on the project document is not cleared and the user sees an inline error; the previously uploaded template remains in effect.

---

### US-003: Replace an Existing Report Template

**As a** QA lead  
**I want to** replace the current report template by uploading a new `.md` file  
**So that** I can update the report format without having to remove the old template first

#### Acceptance Criteria

- [ ] AC-012: When a template is already uploaded the settings page shows a "Replace" action that opens the file upload control.
- [ ] AC-013: On successful upload of a replacement file the new blob is stored, the old blob is deleted, and `reportTemplateUri` is updated atomically on the project document.
- [ ] AC-014: If the new upload succeeds but the old blob deletion fails a system warning is recorded against the project and the new template is still activated; the orphaned blob is flagged for cleanup.
- [ ] AC-015: The same file-type and size validations from US-001 apply to replacement uploads.

---

### US-004: Use Built-In Placeholder Tokens in the Template

**As a** QA lead  
**I want to** use defined placeholder tokens in my Markdown template  
**So that** the report generator substitutes real run data into the correct positions in the rendered report

#### Acceptance Criteria

- [ ] AC-016: The following placeholder tokens are supported and substituted at report generation time:

  | Token | Substituted value |
  |---|---|
  | `{{story_title}}` | Work item title from the PM tool |
  | `{{story_url}}` | Direct URL to the work item in Jira or ADO |
  | `{{run_date}}` | Test run date and time in ISO 8601 UTC format |
  | `{{overall_result}}` | `Passed` or `Failed` |
  | `{{scenarios}}` | Full scenario breakdown (see AC-017) |
  | `{{logs}}` | Step-by-step execution log block (see AC-018, controlled by US-005) |
  | `{{screenshots}}` | Inline screenshot attachments or links (see AC-019, controlled by US-005) |
  | `{{ai_scenario_source}}` | The raw AI-generated scenario text before execution |
  | `{{timing_summary}}` | Total run duration and per-scenario timing |

- [ ] AC-017: `{{scenarios}}` expands to a Markdown table with one row per scenario, columns: Scenario Name, Result (Passed / Failed), Step Count, Duration (ms).
- [ ] AC-018: `{{logs}}` expands to the step-by-step execution log as defined in feature 0005; if logs are disabled for the project (US-005) the token is replaced with an empty string.
- [ ] AC-019: `{{screenshots}}` expands to embedded screenshot images or blob links (one per captured step); if screenshots are disabled or no screenshots were captured the token is replaced with an empty string.
- [ ] AC-020: Tokens are case-sensitive; `{{Story_Title}}` is not recognised and is left as-is in the rendered output.
- [ ] AC-021: Tokens present in the template that have no corresponding data (e.g. `{{screenshots}}` when no screenshots exist) are replaced with an empty string rather than the literal token text.

---

### US-005: Configure Attachment Toggles per Project

**As a** QA lead  
**I want to** independently control whether execution logs and screenshots are included in the generated report  
**So that** I can keep PM tool comments concise or fully detailed depending on my team's review workflow

#### Acceptance Criteria

- [ ] AC-022: The project settings page contains two toggles in the "Report Attachments" subsection:
  - "Include step-by-step logs" (default: ON)
  - "Include screenshots" (default: ON)
- [ ] AC-023: The "Include screenshots" toggle is only enabled when `test_type` is `ui_e2e` or `both`; when `test_type` is `api` the toggle is disabled and a tooltip reads "Screenshots are only available for UI E2E tests".
- [ ] AC-024: When `test_type` is changed to `api` the "Include screenshots" value is coerced to OFF and the coerced value is persisted on the next save.
- [ ] AC-025: Toggle values are stored on the project document in Cosmos DB as `reportIncludeLogs` (boolean, default `true`) and `reportIncludeScreenshots` (boolean, default `true`).
- [ ] AC-026: The API independently validates that `reportIncludeScreenshots` is not `true` when `test_type` is `api`, returning `400 Bad Request` with a `ValidationProblemDetails` body if that combination is submitted.
- [ ] AC-027: Toggle values are pre-populated from the saved project document when the user opens the settings page.

---

### US-006: Generate a Structured Report from the Template at Run Completion

**As the** system  
**I want to** render the project's report template (or the built-in default) using run data at the end of every test run  
**So that** a fully structured, consistently formatted report is ready for delivery to the PM tool and for in-app display

#### Acceptance Criteria

- [ ] AC-028: At the end of every test run the ReportWriter pipeline stage reads the project's `reportTemplateUri` from the project document loaded in Cosmos DB.
- [ ] AC-029: If `reportTemplateUri` is non-null the template is fetched from Azure Blob Storage; if it is null the built-in default template is used.
- [ ] AC-030: The built-in default template includes all supported placeholder tokens in a sensible layout: story metadata at the top, scenario breakdown, then logs, then screenshots.
- [ ] AC-031: Each recognised placeholder token in the template is replaced with the corresponding run data as defined in US-004 before the report is finalised.
- [ ] AC-032: Whether `{{logs}}` expands to real content or an empty string is determined by `reportIncludeLogs`; whether `{{screenshots}}` expands to real content or an empty string is determined by `reportIncludeScreenshots`.
- [ ] AC-033: The rendered report is stored as a blob in Azure Blob Storage and its URI is saved on the test run record in Cosmos DB as `reportBlobUri`.
- [ ] AC-034: If the template blob cannot be fetched (e.g. network error, blob not found) the ReportWriter falls back to the built-in default template, records a system warning against the run, and continues — the run is not failed due to a missing template.
- [ ] AC-035: The rendered report content does not include literal unrecognised tokens; any unknown `{{...}}` pattern is left as-is in the output (per AC-020) and does not cause generation to fail.

---

### US-007: Validate Template Tokens on Upload

**As a** QA lead  
**I want to** be warned about unknown placeholder tokens in my uploaded template  
**So that** I can fix typos or unsupported tokens before they silently produce incomplete reports

#### Acceptance Criteria

- [ ] AC-036: After a template file is successfully stored the API scans the content for all `{{...}}` patterns and compares them against the list of supported tokens defined in US-004.
- [ ] AC-037: If any unknown tokens are found the API returns `200 OK` with the template accepted and a `warnings` array in the response body listing each unrecognised token (e.g. `["{{author}}", "{{build_number}}"]`).
- [ ] AC-038: The UI displays each warning inline below the upload control as a non-blocking notice (e.g. "Unknown token {{author}} — it will appear as-is in the report").
- [ ] AC-039: The presence of warnings does not prevent the template from being saved or used; it is advisory only.
- [ ] AC-040: If all tokens in the uploaded template are recognised no warnings array is returned and no warning notices are shown in the UI.

---

### US-008: Attach the Generated Report to the PM Tool Post

**As a** QA lead  
**I want** the structured report generated from my template to be posted to the originating work item in Jira or Azure DevOps  
**So that** the PM tool comment contains the fully formatted, template-driven report rather than raw unstructured output

#### Acceptance Criteria

- [ ] AC-041: The ReportWriter posts the rendered report content (produced in US-006) as the body of the work item comment in Jira or Azure DevOps, replacing any prior raw log dump behaviour from feature 0005.
- [ ] AC-042: The PM tool comment contains the fully rendered Markdown report with all tokens substituted; no raw placeholder tokens appear in the posted comment.
- [ ] AC-043: If the `test_type` is `ui_e2e` or `both` and `reportIncludeScreenshots` is ON, screenshot blobs are uploaded as attachments to the work item (where supported by the PM tool API) and the `{{screenshots}}` section in the comment contains links to those attachments.
- [ ] AC-044: If the PM tool does not support inline attachments the `{{screenshots}}` section falls back to direct blob storage URLs.
- [ ] AC-045: The in-app Statistics view (feature 0011) renders the same `reportBlobUri` content as the PM tool post — both surfaces use the identical rendered report.
- [ ] AC-046: If posting to the PM tool fails (network error, auth failure) the run is marked with a `ReportDeliveryFailed` status and the rendered report remains accessible in the in-app Statistics view; no data is lost.
