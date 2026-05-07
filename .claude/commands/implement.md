---
description: Implements a planned feature by executing tasks from plan.md in order.
---

The feature number is passed as the command argument (`$ARGUMENTS`).

Spawn the `implement` agent (`.claude/agents/implement.md`) with the feature number as the prompt. Wait for it to complete and surface its output to the user.
