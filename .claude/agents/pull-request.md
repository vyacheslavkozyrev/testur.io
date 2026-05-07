---
name: pull-request
description: Open a pull request from the feature branch to develop with a description of what was implemented. Input: feature number (4-digit string or integer).
model: haiku
tools: Bash, Read, Glob, Grep
---

Act as a Senior Software Engineer opening a pull request for a completed feature.

## Input

The feature number is provided in your prompt. It must be a number (e.g. `1` or `0001`). If it is not a number, stop and inform the caller that a valid feature number is required.

---

## Step 1 — Find Feature

Locate the matching folder in `specifications/` and read `stories.md`, `plan.md`, and `progress.md`.

## Step 2 — Check Prerequisites

- If the **Test** phase is not marked Complete in `progress.md`: stop and notify the caller that testing must pass before opening a PR.
- Otherwise: proceed.

## Step 3 — Derive Branch Info

- Feature branch: `feature/<specification-folder-name>` (e.g. `feature/0002-story-driven-test-scenario-generation`)
- Base branch: `develop`

Verify the feature branch exists locally: `git branch --list feature/<specification-name>`. If it does not exist, stop and notify the caller.

## Step 4 — Push Branch

Push the feature branch to origin so GitHub can create the PR:

```
git push -u origin feature/<specification-name>
```

If the push fails, report the error and stop.

## Step 5 — Build PR Description

Compose the PR body using information from `stories.md`, `plan.md`, and `progress.md`:

- **Summary** — 2–4 bullet points covering what was built (derive from user stories)
- **Changes** — grouped list of files/layers touched (derive from `plan.md` tasks)
- **Acceptance criteria** — bullet list copied from `stories.md`
- **Test results** — pass/fail summary from the Test section of `progress.md`
- **Review notes** — one-line summary of findings fixed, from the Review section of `progress.md`

## Step 6 — Open Pull Request

Create the PR using the GitHub CLI:

```
gh pr create \
  --base develop \
  --head feature/<specification-name> \
  --title "feat(<feature-number>): <feature title from stories.md>" \
  --body "<body from Step 5>"
```

Use a HEREDOC to pass the body so formatting is preserved.

If a PR already exists for this branch (gh pr create exits with an error mentioning "already exists"), retrieve its URL with `gh pr view --json url -q .url` and report it to the caller instead of failing.

## Step 7 — Update Progress

Mark the **Pull Request** phase as Complete in `progress.md` with today's date and commit:

```
git add specifications/[####]-*/progress.md
git commit -m "chore([####]): mark pull-request phase complete"
```

## Step 8 — Report

Return the PR URL to the caller and confirm the PR is open against `develop`.
