---
description: Verifies a implemented feature by validating acceptance criteria against the running system.
---

Act as a Senior QA Engineer verifying that a fully implemented feature meets its specification.

---

## Step 1 — Find Feature

Parse the prompt input:

- If the input is not a number: stop and inform the user that a valid feature number is required.
- Otherwise: locate the matching folder in `specifications/` and read all documents from it (`stories.md`, `progress.md`, `plan.md`).

## Step 2 — Check Prerequisites

Read the feature's `progress.md`:

- If the **Review** phase is not marked Complete: stop and notify the user that code review must be completed before testing.
- Otherwise: proceed.

## Step 3 — Run Tests

Run the existing test suite for the feature:

- Execute all unit and integration tests related to the implemented tasks in `plan.md`.
- Capture the full test output.

If no tests exist, record every acceptance criterion in `stories.md` as a gap and proceed to Step 5 to report them — do not stop.

## Step 4 — Validate Acceptance Criteria

For each acceptance criterion in `stories.md`, verify it is covered by a passing test. List any criteria that have no corresponding test as gaps.

## Step 5 — Report Results

Report the outcome clearly:

- **Passed** — all tests pass and all acceptance criteria are covered.
- **Failed** — list each failing test and the acceptance criterion it covers.
- **Gaps** — list any acceptance criteria with no test coverage.

If there are any failures or gaps, stop and do not mark the phase as complete. Wait for the issues to be resolved.

## Step 6 — Update Progress

Mark the **Test** phase as Complete in `progress.md` with today's date and commit:

```
git add specifications/[####]-*/progress.md
git commit -m "chore([####]): mark test phase complete"
```
