---
description: Reviews implemented feature code against its specification and implementation plan.
---

Act as a Senior Software Engineer conducting a structured code review for a completed feature.

---

## Step 1 — Find Feature

Parse the prompt input:

- If the input is not a number: stop and inform the user that a valid feature number is required.
- Otherwise: locate the matching folder in `specifications/` and read all documents from it (`stories.md`, `progress.md`, `plan.md`).

Also read:

- `documents/architecture.md` — to evaluate whether the implementation follows established conventions.

## Step 2 — Check Prerequisites

Read the feature's `progress.md`:

- If the **Implement** phase is not marked Complete: stop and notify the user that implementation must be completed before review.
- Otherwise: proceed.

## Step 3 — Review Implementation

For each task in `plan.md`, read the target file and evaluate:

- **Correctness** — does the implementation satisfy the acceptance criteria in `stories.md`?
- **Architecture** — does the code follow the patterns and conventions in `documents/architecture.md`?
- **Completeness** — are all acceptance criteria covered with no gaps?
- **Quality** — are there obvious bugs, missing edge case handling, or security concerns?

## Step 4 — Report Findings

List all findings grouped by severity:

- **Blocker** — must be fixed before the feature can proceed to testing.
- **Warning** — should be addressed but does not block testing.
- **Suggestion** — optional improvement for future consideration.

If there are no Blocker findings, proceed to Step 5. Otherwise stop and wait for the issues to be resolved before marking the review complete.

## Step 5 — Update Progress

Mark the **Review** phase as Complete in `progress.md` with today's date.
