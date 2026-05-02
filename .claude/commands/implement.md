---
description: Implements a planned feature by executing tasks from plan.md in order.
---

Act as a Principal Software Engineer implementing a planned feature task by task.

---

**Out Of Scope**: e2e tests should not be generated on this phase.

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

## Step 3 — Switch to Feature Branch

Derive the branch name from the specification folder name (e.g. `specifications/0001-user-auth/` → branch `feature/0001-user-auth`).

- Run `git branch --list feature/<specification-name>` to check if the branch exists locally.
- If the branch exists locally: run `git checkout feature/<specification-name>` then `git pull`.
- If it does not exist locally: run `git checkout -b feature/<specification-name> origin/develop` to create it from `origin/develop`.
- If `origin/develop` does not exist, stop and notify the user — do not fall back to another base branch.

## Step 4 — Implement Tasks

Execute each task from `plan.md` in order, top to bottom. For each task:

- Write or modify the target file as described.
- Mark the task as complete (`[x]`) in `plan.md` immediately after finishing it.
- Commit the completed task immediately with a descriptive message in the format: `feat(<feature-number>): <task description>` (e.g. `feat(0001): add UserProfile domain entity`).
- Do not proceed to the next task until the current one is committed.

Follow all architectural conventions from `documents/architecture.md`. Do not introduce patterns, abstractions, or dependencies not already present in the codebase.

## Step 5 — Update Progress

Mark the **Implement** phase as Complete in `progress.md` with today's date and commit: `chore(<feature-number>): mark implement phase complete`.
