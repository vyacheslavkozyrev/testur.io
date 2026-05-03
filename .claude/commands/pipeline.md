---
description: End-to-end feature development workflow — runs specify, plan, implement, review, and test in sequence for a given feature number.
---

The feature number is passed as the command argument (`$ARGUMENTS`). If it is not a number, stop immediately and inform the user that a valid feature number is required.

Execute each step below in sequence by spawning the corresponding agent. Pass the feature number unchanged to every agent.

---

## Step 0 — Research (optional)

If `documents/features.md` does not exist, spawn the `research` agent (`.claude/agents/research.md`) with no arguments and wait for it to complete. Then ask the user to confirm the feature list before proceeding.

If `documents/features.md` already exists, skip this step.

## Step 1 — Specify

Spawn the `specify` agent (`.claude/agents/specify.md`) with the feature number as the prompt.

Wait for the agent to complete. Show the user a summary of the specification produced, then ask for confirmation before proceeding to the next step.

## Step 2 — Plan

Spawn the `plan` agent (`.claude/agents/plan.md`) with the feature number as the prompt.

Wait for the agent to complete. Show the user a summary of the implementation plan produced, then ask for confirmation before proceeding to the next step.

## Step 3 — Implement

Spawn the `implement` agent (`.claude/agents/implement.md`) with the feature number as the prompt.

Wait for the agent to complete and all files to be written before proceeding to the next step.

## Step 4 — Code Review

Spawn the `review` agent (`.claude/agents/review.md`) with the feature number as the prompt.

Wait for the review, all automated fixes, and the commit to complete before proceeding to the next step.

## Step 5 — Test

Spawn the `test` agent (`.claude/agents/test.md`) with the feature number as the prompt.

Wait for the agent to complete. Report the final test outcome to the user. If tests fail, surface the failures clearly and stop — do not mark the pipeline as complete until all tests pass.
