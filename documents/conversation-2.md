# Conversation 2 — AI Agents, Test Types, Memory Layer

**Date:** 2026-05-09

---

**User:** let's discuss our system prompts and agents and tools structure. I want testurio be able to generate test scenarios for all possible test types. Please tell me what test types, strategies do you know?

**Assistant:**

## Software Testing Types & Strategies

Here's a comprehensive overview of what's relevant for an AI-powered test generation platform:

### Functional Testing
| Type | What it validates | Tool fit |
|------|------------------|----------|
| **Unit** | Single function/class in isolation | Not applicable (needs source code) |
| **Integration** | Service-to-service contracts | HTTP client |
| **API / Contract** | REST/GraphQL endpoints, schema, status codes | HTTP client |
| **UI E2E** | User flows in a real browser | Playwright |
| **Smoke** | Core paths work after deploy | Playwright + HTTP |
| **Regression** | Nothing broke after a change | Replay corpus |
| **Acceptance (BDD)** | Story criteria satisfied | Gherkin → Playwright |

### Non-Functional Testing
| Type | What it validates | Tool fit |
|------|------------------|----------|
| **Performance / Load** | Throughput, latency under load | k6, Artillery |
| **Accessibility (a11y)** | WCAG compliance | axe-core + Playwright |
| **Visual regression** | Pixel-level UI diff | Playwright screenshots |
| **Security / DAST** | OWASP vulnerabilities from outside | OWASP ZAP, nuclei |
| **Compatibility** | Cross-browser, cross-device | Playwright multi-browser |
| **SEO / metadata** | Meta tags, canonical, structured data | HTTP client |

### AI-Specific Consideration
For Testurio the most **immediately valuable** expansion over your current `api | ui_e2e | both`:

1. **Smoke** — subset of E2E, runs after every deploy
2. **Accessibility** — high demand, Playwright has native axe integration
3. **Visual regression** — Playwright screenshots + diff
4. **Performance** — k6 scenarios can be generated from the same story context

**My recommendation:** expand `test_type` to `api | ui_e2e | smoke | a11y | visual | performance | both`, with `smoke` and `a11y` being lowest-effort additions since they reuse the Playwright executor. Performance would need a separate k6 executor.

What angle matters most to you — breadth of coverage types, or depth in generating high-quality scenarios for the ones you already have?

---

**User:** ok, so based on test types I think we can build AI agents to support each type? what do you think?

**Assistant:**

Yes, exactly — and it maps very cleanly to the architecture you already have.

## Agent-per-Test-Type Model

Each test type gets its own **TestGeneratorPlugin** variant and **TestExecutorPlugin** variant:

```
StoryParser  →  [Agent Router]  →  ApiTestGenerator
                                →  UiE2eTestGenerator
                                →  SmokeTestGenerator
                                →  A11yTestGenerator
                                →  VisualTestGenerator
                                →  PerformanceTestGenerator
                    ↓
               [Executor Router] →  HttpExecutor        (API, Smoke)
                                →  PlaywrightExecutor   (UI E2E, A11y, Visual)
                                →  K6Executor           (Performance)
                    ↓
               ReportWriter
```

## Why This Works Well

- **Generators differ significantly** — an API test generator needs to reason about HTTP verbs, status codes, and payloads; a visual test generator needs to reason about viewports, states, and baseline management. Separate prompts per agent = higher quality output.
- **Executors are mostly shared** — A11y and Visual reuse Playwright, just with different Playwright APIs (`page.accessibility.snapshot()`, `expect(page).toHaveScreenshot()`). You get reuse at the executor layer.
- **Routing is simple** — the `test_type` field on the project config determines which generator + executor combination fires.

## The Key Design Question

Do you want **one agent call per story** that generates all applicable scenario types, or **parallel agent calls** — one per test type — running concurrently?

Parallel gives you better quality (focused prompts) and faster wall-clock time. That would be the natural next design decision to make before specifying the pipeline.

---

**User:** great, so far we are on the same page. Also I want to have memory layer so we can improve test scenario generation quality. What would you recommend for this? Keep it as MD, as RAG or something else? I see it as once test run pass, use mark most effective scenario and we move story and this scenario into memory layer. Thoughts?

**Assistant:**

## Memory Layer Recommendation: RAG in Cosmos DB

**Short answer: RAG, not Markdown.** Here's why and how:

### Why Not Markdown
- No semantic search — you'd miss "login flow" matching "authentication scenario"
- Doesn't scale past a few hundred examples
- Hard to scope per-project vs cross-project

### Why RAG Fits Perfectly

Your memory is essentially **few-shot examples** injected into the generator prompt. That's the classic RAG pattern:

```
New story arrives
      ↓
Embed story text  →  Vector search Cosmos  →  Top-3 similar past scenarios
                                                       ↓
                              TestGenerator prompt: "Here are proven examples..."
                                                       ↓
                                               Generated scenarios
```

### Storage: Cosmos DB Vector Search

Azure Cosmos DB now supports **DiskANN vector indexing** natively — no new infrastructure, fits your existing stack:

```
MemoryContainer (Cosmos)
├── id
├── userId          ← tenant isolation preserved
├── projectId       ← optional: project-scoped vs global memory
├── storyEmbedding  ← float[] from embedding model
├── storyText
├── scenarioText    ← the winning scenario
├── testType        ← api | ui_e2e | a11y | etc.
├── passRate        ← 0.0–1.0, updated on repeated use
└── createdAt
```

### Embedding Model

Anthropic doesn't provide embeddings — use **Azure OpenAI `text-embedding-3-small`** (cheap, fast, already in Azure ecosystem).

### The Feedback Loop

```
Test run completes
      ↓
All scenarios pass?  →  Mark scenario as "effective"
      ↓
Generate embedding of story
      ↓
Upsert into MemoryContainer (or increment passRate if exists)
```

### Key Design Decisions to Make

1. **Scope** — per-project memory only, or cross-project (anonymized)? Cross-project gives you network effects but raises data sensitivity questions.
2. **Retrieval count** — top-3 similar examples is usually the sweet spot before prompt bloat.
3. **Decay** — do old passing scenarios lose weight over time, or is passRate enough?

Cross-project memory scoped by `testType` would be your biggest quality multiplier — a passing login scenario from one project teaches the generator for all projects.

---

**User:** ok, based on this build prompts agents tools memory schema

**Assistant:**

Created `documents/ai-agents.md` with the full design. Here's the summary:

**7 agents:** StoryParser, ApiTestGenerator, UiE2eTestGenerator, SmokeTestGenerator, A11yTestGenerator, VisualTestGenerator, PerformanceTestGenerator + ReportWriter

**3 tools:** `retrieve_memory` (vector search before generation), `store_memory` (persist on pass), `get_project_config` (load project settings into agent context)

**Memory schema:** Cosmos `TestMemory` container with DiskANN vector index on `storyEmbedding` (1536-dim, Azure OpenAI `text-embedding-3-small`), scoped by `userId` + `testType`, with `passRate` decay for low-quality entries

**Key design choices:**
- Memory retrieval happens **before** generation — top-3 similar past scenarios are injected as few-shot examples into each generator prompt
- Generators run **in parallel** per test type
- Cross-project anonymized sharing is opt-in per user
- Each agent has a focused system prompt with explicit output JSON schema — no ambiguity for Claude to produce parseable output

Full details in `documents/ai-agents.md`.

---

**User:** please save whole conversation into conversation-2.md file including my questions
