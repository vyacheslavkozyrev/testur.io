# Progress — Real-Life E2E Test Suite for the Web Portal (0049)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-06-13 |       |
| Plan      | ✅ Complete | 2026-06-13 |       |
| Implement | ⏳ Pending  |            |       |
| Review    | ⏳ Pending  |            |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review

_Populated by `/review [####]`_

---

## Test Results

_Populated by `/test [####]`_

---

## Amendments

### Amendment — 2026-06-12
**Changed**: `stories.md` (added US-011, US-012, US-013), `plan.md` (rewritten — 12 tasks, 1:1 with stories)
**Reason**: Three missing stories added (new user registration, forgot-password/reset, post-suite DB teardown); task list was not granular enough (7 tasks for 10 stories); tasks now map 1:1 to user stories and include teardown wiring in the config task
**Impact**: Plan phase re-run to produce updated task list; Implement phase not yet started so no rework required

### Amendment — 2026-06-13
**Changed**: `stories.md` (full rewrite — 43 stories, AC-001–AC-186), `plan.md` (full rewrite — T000–T024, 25 tasks)
**Reason**: Comprehensive review of all UI-facing specifications (0006, 0007, 0008, 0009, 0010, 0010a, 0010b, 0010c, 0011, 0012, 0013, 0014, 0015, 0016, 0017, 0020) revealed major coverage gaps. Added: sign-in wrong-password error state, route guard (5 protected routes), registration duplicate-email error, forgot-password flow, pricing page plan display, billing status after purchase, dashboard overview + card navigation, project list with seed and empty state, card edit icon navigation, project create validation errors, settings tab pre-population, settings tab save validation, report format/attachments section, integration tab ADO and Jira form details, access mode IP/Basic Auth/Header Token all three modes, work item type filter, project delete with confirmation, test history populated state with trend chart and run detail panel, sidebar active highlight, header identity, sidebar collapse/expand, account settings display name + preferences. Renumbered all stories US-001–US-043 and acceptance criteria AC-001–AC-186. Task plan expanded from 12 to 25 tasks with 1 marked Done (subscription.spec.ts). Tasks now ordered by user journey dependency and each maps to one or more stories with explicit file paths.
**Impact**: Implement phase not yet started so no rework required; task numbering restarted from T000
