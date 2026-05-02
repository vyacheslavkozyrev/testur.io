---
description: End-to-end feature development workflow — runs specify, plan, implement, review, and test in sequence for a given feature number.
---

You are acting as an automated development agent. Execute each step below in sequence for the feature number provided in the input. If the input is not a number, stop immediately and inform the user that a valid feature number is required.

The feature number is passed to every sub-command unchanged.

---

## Step 0 — Research (optional)

If `documents/features.md` does not exist, call `/research` first and wait for the user to confirm the feature list before proceeding.

If `documents/features.md` already exists, skip this step.

## Step 1 — Specify

Call `/specify` with the feature number.

Wait for the specification to be completed and the user to confirm before proceeding to the next step.

## Step 2 — Plan

Call `/plan` with the feature number.

Wait for the implementation plan to be completed and the user to confirm before proceeding to the next step.

## Step 3 — Implement

Call `/implement` with the feature number.

Wait for implementation to be completed and all files written before proceeding to the next step.

## Step 4 — Code Review

Call `/review` with the feature number.

Wait for the review, all automated fixes, and the commit to complete before proceeding to the next step.

## Step 5 — Test

Call `/test` with the feature number.

Report the final test outcome to the user. If tests fail, surface the failures clearly and stop — do not mark the pipeline as complete until all tests pass.
