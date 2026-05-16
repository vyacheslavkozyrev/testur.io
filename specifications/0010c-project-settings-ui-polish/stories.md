# User Stories — Project Settings Page — UI Polish & Tab Layout (0010c)

## Out of Scope

The following are explicitly **not** part of this feature:

- Changes to any backend API contracts or data models
- Modifying the Integration forms themselves (PM tool connect / disconnect flows remain unchanged)
- Adding new settings sections or fields
- Persisting tab selection across page reloads
- Browser-level "leave page?" confirmation dialogs (navigation guard on tab/browser close)
- Toast notification infrastructure (inline feedback on the page is sufficient)

---

## Stories

### US-001: Two-Tab Layout

**As a** QA lead  
**I want to** navigate the project settings page via two clearly labelled tabs — **Settings** and **Integration**  
**So that** I can find what I need instantly without scrolling through a single long page

#### Acceptance Criteria

- [ ] AC-001: The project settings page renders two tabs: "Settings" (active by default) and "Integration".
- [ ] AC-002: Clicking "Integration" replaces the settings content area with the existing PM tool integration form (`IntegrationPage` content), without navigating away from the page.
- [ ] AC-003: Clicking "Settings" returns to the full settings form.
- [ ] AC-004: The active tab is visually highlighted.
- [ ] AC-005: Tab labels are localised via `i18next`.

---

### US-002: Unified Save Button

**As a** QA lead  
**I want to** save all my changes with a single "Save Changes" button at the bottom of the Settings tab  
**So that** I don't have to hunt for multiple buttons across different sections

#### Acceptance Criteria

- [ ] AC-006: The Settings tab has exactly one "Save Changes" primary button, positioned at the bottom of the form content.
- [ ] AC-007: The per-section Save buttons in `ProjectForm` (edit mode) and `ReportSettingsSection` are removed; their functionality is absorbed by the unified button.
- [ ] AC-008: Clicking "Save Changes" triggers all necessary API calls in sequence: project info, custom prompt, report settings.
- [ ] AC-009: If `ProjectForm` field validation fails, the save is aborted before any API call is made and validation errors are shown inline on the offending fields.
- [ ] AC-010: The "Save Changes" button shows a loading spinner and is disabled while any API call is in flight.
- [ ] AC-011: The "Save Changes" button is disabled when no fields have changed from the last saved values (clean state).

---

### US-003: Save Feedback — Success

**As a** QA lead  
**I want to** see a clear success confirmation after saving  
**So that** I know my changes were persisted and can move on confidently

#### Acceptance Criteria

- [ ] AC-012: After all API calls succeed, the button transitions briefly to a "Saved ✓" state (green, disabled) for 2 seconds, then reverts to the normal disabled state (no unsaved changes).
- [ ] AC-013: The button label returns to "Save Changes" (enabled) only when the user makes a further change.

---

### US-004: Save Feedback — Partial or Full Failure

**As a** QA lead  
**I want to** know which section failed to save when an error occurs  
**So that** I can retry only the relevant section or understand what went wrong

#### Acceptance Criteria

- [ ] AC-014: If any API call fails, an error alert appears below the failing section (project info, custom prompt, or report settings), identifying which section failed.
- [ ] AC-015: Sections that saved successfully are not shown an error.
- [ ] AC-016: The "Save Changes" button returns to the enabled state after a failure so the user can retry.
- [ ] AC-017: Retry: clicking "Save Changes" again re-attempts only the sections that failed (sections that already succeeded are not re-posted).

---

### US-005: Unsaved Changes Indicator

**As a** QA lead  
**I want to** see at a glance whether I have unsaved changes  
**So that** I don't switch tabs or leave the page thinking everything is saved

#### Acceptance Criteria

- [ ] AC-018: When any field in the Settings tab differs from its last-saved value, the "Save Changes" button becomes enabled.
- [ ] AC-019: When no field has changed, the button is disabled and shows "No changes" as its label (or equivalent localised text).
- [ ] AC-020: Switching to the Integration tab while there are unsaved changes shows a subtle inline banner above the tab bar: "You have unsaved changes in Settings." The banner links back to the Settings tab.
- [ ] AC-021: The unsaved-changes banner disappears once changes are saved or discarded.

---

### US-006: Consistent Form Field Width

**As a** QA lead  
**I want to** all form fields in the settings page to span the same full width  
**So that** the page looks polished and easy to scan

#### Acceptance Criteria

- [ ] AC-022: The `ProjectForm` component no longer applies a `maxWidth: 640` constraint in edit mode; fields stretch to the full container width (matching all other sections).
- [ ] AC-023: Custom Prompt, report attachment toggles, and report template upload all align with the project info fields.

---

### US-007: Consistent Section Heading Size

**As a** QA lead  
**I want to** all section titles on the settings page to use the same typographic level  
**So that** the page has a clear, uniform visual hierarchy

#### Acceptance Criteria

- [ ] AC-024: The "Edit Project" section title (currently `h5`) is changed to `h6`, matching "Custom Test Generation Prompt", "Report Format & Attachment Settings", and "Danger Zone".
- [ ] AC-025: No other section heading on the Settings tab exceeds `h6`.
