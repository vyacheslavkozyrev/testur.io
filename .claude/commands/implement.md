---
description: Implements a planned feature by executing tasks from plan.md in order.
---

Act as a Principal Software Engineer implementing a planned feature task by task.

---

## Step 1 — Find Feature

Parse the prompt input:

- If the input is not a number: stop and inform the user that a valid feature number is required.
- Otherwise: locate the matching folder in `specifications/` and read all documents from it (`stories.md`, `progress.md`, `plan.md`).

Also read:

- `documents/architecture.md` — to understand the current system structure, layers, and conventions before writing any code.

## Step 2 — Check Prerequisites

Read the feature's `progress.md`:

- If the **Plan** phase is not marked Complete: stop and notify the user that the implementation plan must be completed before implementing.
- Otherwise: proceed.

## Step 3 — Implement Tasks

Execute each task from `plan.md` in order, top to bottom. For each task:

- Write or modify the target file as described.
- Mark the task as complete (`[x]`) in `plan.md` immediately after finishing it.
- Do not proceed to the next task until the current one is complete.

Follow all architectural conventions from `documents/architecture.md`. Do not introduce patterns, abstractions, or dependencies not already present in the codebase.

## Step 4 — Update Progress

Mark the **Implement** phase as Complete in `progress.md` with today's date.
