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
- If a **Review** section already exists in `progress.md`: warn the user that a previous review was recorded and ask whether to proceed and overwrite it.
- Otherwise: proceed.

## Step 3 — Pre-flight Checks

Before spawning the review agent:

1. **File existence** — for every file path referenced in `plan.md`, verify the file exists. List any missing files and treat each as a Blocker finding (skip to Step 4 with these pre-populated); do not spawn the agent until the user confirms how to proceed.

2. **Diff size guard** — run `git diff develop...HEAD --stat` and count the total lines changed. If the total exceeds 500 lines, report the line count to the user and ask for confirmation before continuing.

## Step 4 — Review Implementation

Spawn the `code-reviewer` agent (`.claude/agents/code-reviewer.md`) with this context:

- Feature number and title
- Full content of `stories.md`
- Relevant sections of `documents/architecture.md`

The agent will run `git diff develop...HEAD` itself and read the rule files. Collect all findings it returns and pass them to Step 5.

## Step 5 — Fix All Findings

For every finding returned by the agent (Blocker, Warning, or Suggestion):

1. Apply the fix described in the finding directly to the source file.
2. After all fixes are applied, re-run the `code-reviewer` agent with the same context to verify no new issues were introduced.
3. If the re-review produces new findings, apply those fixes and re-run once more (maximum **3 iterations** total). If findings still remain after 3 iterations, stop fixing and record the remaining issues in `progress.md` under a **Remaining issues** section for manual resolution.

Do not stop or ask for confirmation between fixes — fix everything autonomously within the iteration cap.

## Step 6 — Commit Fixes

If any fixes were applied, commit the changes on the current feature branch:

```
git add -A
git commit -m "review: fix code review findings for feature #<feature_number>"
```

Do not push.

## Step 7 — Update Progress

Append a **Review** section to `progress.md` with today's date in this format:

```
## Review — <date>

### Blockers fixed
- <file>:<line> — <one-line summary of issue and fix applied>

### Warnings fixed
- <file>:<line> — <one-line summary>

### Suggestions fixed
- <file>:<line> — <one-line summary>

### Remaining issues (if any)
- <file>:<line> — <one-line summary> — requires manual resolution

### Status: Complete
```

If a severity category has no findings, omit that section. Then mark the **Review** phase as Complete.
