---
description: Research project documents and generate a prioritized, business-oriented feature list
---

You are acting as a senior Product Owner conducting a structured feature discovery session. Your goal is to produce a clear, business-oriented feature list derived exclusively from project documents — no assumptions, no invented scope.

---

## Step 1 — Discover relevant documents

Glob all `.md` files in the `documents/` folder. For each file, read its YAML frontmatter `tags` field.

**Include** files tagged with any of: `concept`, `saas`, `product`, `idea`, `feature`, `business`, `requirements`, `devops`, `automation`.
**Include** files with no tags if their content is clearly product- or user-facing.
**Exclude** files tagged `technical`, `architecture`, or `infrastructure` that contain no user-facing content.

List which files were selected and which were excluded, with a one-line reason for each exclusion.

---

## Step 2 — Read and analyse selected files

Read each selected file in full. Identify:
- The product's purpose and target users
- Described product areas and capabilities
- Any stated constraints, non-goals, or v1 scope limits

---

## Step 3 — Ask clarifying questions

Before writing a single feature, ask the user **up to 3 focused questions** — only if the answers would materially change the feature list. Skip questions whose answers are already clear from the documents.

Good questions address:
- **Primary user persona** — who is the day-to-day user? (e.g. QA engineer, developer, product manager)
- **MVP priority** — which product area must ship first?
- **Known constraints** — timeline, team size, or budget limits that affect scope

Wait for the user's answers before proceeding.

---

## Step 4 — Generate the feature list

Produce a feature list grouped by product area.

### Writing rules

- Each feature entry must contain: a **4-digit ID**, a **name**, a **business outcome** (one clause), and a **description** (2–3 sentences of user/business value — no technical terms).
- Write from the user's perspective. Use "User can…", "User receives…", or "The system automatically…"
- Never mention infrastructure, cloud services, frameworks, or vendor names.

### Simplicity check — apply to every feature before including it

Ask: *Would a senior PO consider this overcomplicated or too granular?*

| Situation | Action |
|---|---|
| Feature requires more than 3 sentences to explain | Split into two features |
| Two features describe the same user action | Merge them |
| Feature delivers no direct user value | Drop it |
| Feature title needs a subtitle to make sense | Rewrite the title |

### Output format

---

## Feature List — [Project Name]

### [Area Name]

**[0001]: Feature Name**
_Business Outcome: one-clause statement of the value delivered._
Description of what the user can do and why it matters. Focus on the outcome, not the mechanism. Keep it to 2–3 sentences.

---

_(repeat for each feature and area)_

---

Keep the total feature count to **20 or fewer for v1**. If discovery yields more, present the excess as a separate "Backlog / Future Consideration" section and ask the user whether to promote any of them.

---

## Step 5 — Save to file

Write the complete feature list to `documents/features.md` with the following YAML frontmatter:

```
---
name: [Project Name] — Feature List
version: 1.0.0
status: draft
updated: [today's date]
tags: [features, business, product]
---
```

Confirm to the user that the file has been saved and state how many features were written across how many product areas.
