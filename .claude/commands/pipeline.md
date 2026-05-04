---
description: End-to-end feature development workflow — runs specify, plan, implement, review, and test in sequence for a given feature number.
---

The feature number is passed as the command argument (`$ARGUMENTS`). If it is not a number, stop immediately and inform the user that a valid feature number is required.

Execute each step below in sequence by spawning the corresponding agent. Pass the feature number unchanged to every agent.

---

## Step 0 — Research (optional)

If `documents/features.md` does not exist, spawn the `research` agent (`.claude/agents/research.md`) with no arguments and wait for it to complete. Then ask the user to confirm the feature list before proceeding.

If `documents/features.md` already exists, skip this step.

## Step 1 — Pre-read Architecture

Read `documents/architecture.md`. Extract the **layer tag table** and the **implementation layer order** section (the table mapping tag to scope under "Implementation Layer Order"). Store this as `architectureLayerSummary`. You will inject it into the prompts for Steps 1, 2, and 3 so those agents do not need to re-read the full file.

## Step 2 — Specify + Plan

Spawn the `specify-and-plan` agent (`.claude/agents/specify-and-plan.md`) with the feature number as the prompt. Append the `architectureLayerSummary` extracted in Step 0b to the prompt.

Wait for the agent to complete both the Specify and Plan phases before proceeding to the next step.

## Step 3 — Implement

Spawn the `implement` agent (`.claude/agents/implement.md`) with the feature number as the prompt. Append the `architectureLayerSummary` to the prompt.

Wait for the agent to complete and all files to be written before proceeding to the next step.

## Step 4 — Code Review

Spawn the `review` agent (`.claude/agents/review.md`) with the feature number as the prompt. Append the `architectureLayerSummary` to the prompt.

Wait for the review, all automated fixes, and the commit to complete before proceeding to the next step.

## Step 5 — Test

Spawn the `test` agent (`.claude/agents/test.md`) with the feature number as the prompt.

Wait for the agent to complete. Report the final test outcome to the user. If tests fail, surface the failures clearly and stop — do not mark the pipeline as complete until all tests pass.
