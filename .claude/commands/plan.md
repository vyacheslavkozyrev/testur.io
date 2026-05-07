---
description: Reviews specification documentation and generates a sequential implementation plan task by task.
---

The feature number is passed as the command argument (`$ARGUMENTS`).

Spawn the `plan` agent (`.claude/agents/plan.md`) with the feature number as the prompt. Wait for it to complete and surface its output to the user.
