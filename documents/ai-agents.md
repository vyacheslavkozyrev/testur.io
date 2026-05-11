# Testurio — AI Agents, Prompts, Tools & Memory

## Overview

The pipeline has two orchestrated layers:

```
Webhook
  │
  ▼
StoryParserAgent          — extracts structured story from raw ADO/Jira text
  │
  ▼
MemoryRetrievalTool       — vector search: top-3 similar past scenarios per test type
  │
  ▼
[Parallel Generator Agents — one per enabled test_type]
  ├── ApiTestGeneratorAgent
  ├── UiE2eTestGeneratorAgent
  ├── SmokeTestGeneratorAgent
  ├── A11yTestGeneratorAgent
  ├── VisualTestGeneratorAgent
  └── PerformanceTestGeneratorAgent
  │
  ▼
[Executor Router]
  ├── HttpExecutor          (api, smoke)
  ├── PlaywrightExecutor    (ui_e2e, a11y, visual)
  └── K6Executor            (performance)
  │
  ▼
MemoryWriterTool          — stores effective scenarios on pass
  │
  ▼
ReportWriterAgent         — posts results to ADO / Jira
```

---

## Memory Schema

### Cosmos DB Container: `TestMemory`

Partition key: `userId`

```json
{
  "id": "uuid-v4",
  "userId": "b2c-oid",
  "projectId": "uuid | null",
  "testType": "api | ui_e2e | smoke | a11y | visual | performance",
  "storyEmbedding": [0.021, -0.104, ...],
  "storyText": "As a user I want to log in with email and password...",
  "storyKeywords": ["login", "authentication", "email", "password"],
  "scenarioText": "{ ... serialized scenario JSON ... }",
  "passRate": 1.0,
  "runCount": 3,
  "lastUsedAt": "2026-05-09T10:00:00Z",
  "createdAt": "2026-04-01T08:00:00Z"
}
```

**Vector index config (Cosmos DiskANN):**

```json
{
  "vectorEmbedding": {
    "path": "/storyEmbedding",
    "dataType": "float32",
    "dimensions": 1536,
    "distanceFunction": "cosine"
  }
}
```

**Notes:**
- `projectId: null` = cross-project memory (anonymized, opt-in per user)
- `passRate` incremented on each reuse that results in a pass; decremented on fail
- Scenarios with `passRate < 0.5` after `runCount >= 5` are soft-deleted

---

## Tool Definitions

Tools are passed to Claude agents via the Anthropic SDK `Tools` parameter.

### `retrieve_memory`

Fetches the top-k most semantically similar past scenarios.

```csharp
new Tool
{
    Name = "retrieve_memory",
    Description = "Retrieve the most relevant past test scenarios from memory based on semantic similarity to the current story. Call this before generating scenarios.",
    InputSchema = new InputSchema
    {
        Type = "object",
        Properties = new
        {
            story_text = new { type = "string", description = "The parsed story text to search against." },
            test_type  = new { type = "string", enum = new[] { "api", "ui_e2e", "smoke", "a11y", "visual", "performance" } },
            top_k      = new { type = "integer", description = "Number of examples to return. Default 3." }
        },
        Required = new[] { "story_text", "test_type" }
    }
}
```

**Handler:** embeds `story_text` via Azure OpenAI `text-embedding-3-small`, runs Cosmos vector query scoped to `userId` and `testType`, returns top-k serialized scenarios.

---

### `store_memory`

Persists an effective scenario after a test run passes.

```csharp
new Tool
{
    Name = "store_memory",
    Description = "Store a passing test scenario in memory for future reuse. Call only after a test run passes.",
    InputSchema = new InputSchema
    {
        Type = "object",
        Properties = new
        {
            story_text    = new { type = "string" },
            scenario_json = new { type = "string", description = "Serialized scenario JSON." },
            test_type     = new { type = "string", enum = new[] { "api", "ui_e2e", "smoke", "a11y", "visual", "performance" } },
            project_id    = new { type = "string", description = "Project UUID." },
            share_globally = new { type = "boolean", description = "Contribute anonymized example to cross-project memory." }
        },
        Required = new[] { "story_text", "scenario_json", "test_type", "project_id" }
    }
}
```

---

### `get_project_config`

Loads project settings needed by generator and executor agents.

```csharp
new Tool
{
    Name = "get_project_config",
    Description = "Fetch the project configuration including product URL, auth settings, and enabled test types.",
    InputSchema = new InputSchema
    {
        Type = "object",
        Properties = new
        {
            project_id = new { type = "string" }
        },
        Required = new[] { "project_id" }
    }
}
```

**Returns:**

```json
{
  "product_url": "https://staging.example.com",
  "test_types":  ["api", "ui_e2e"],
  "access_mode": "ip_allowlist",
  "api_base_url": "https://staging-api.example.com/v1"
}
```

---

## Agent System Prompts

All agents receive adaptive thinking. Token budgets are per-call estimates.

---

### StoryParserAgent

**MaxTokens:** 4000 | **Model:** `claude-opus-4-7`

```
You are a test analysis agent. Your job is to parse a raw user story or ticket from a project management tool and extract structured information for test generation.

Extract:
- title: short summary of the story
- description: what the feature does
- acceptance_criteria: list of specific, testable conditions
- entities: key domain objects involved (e.g. User, Order, Product)
- actions: verbs describing what the user or system does
- edge_cases: boundary conditions or error paths mentioned or implied

Output valid JSON only. No explanation text outside the JSON block.

Output schema:
{
  "title": "string",
  "description": "string",
  "acceptance_criteria": ["string"],
  "entities": ["string"],
  "actions": ["string"],
  "edge_cases": ["string"]
}
```

---

### ApiTestGeneratorAgent

**MaxTokens:** 16000 | **Model:** `claude-opus-4-7`

```
You are an API test generation agent. You generate HTTP-based test scenarios from user stories.

You have access to the following tools:
- retrieve_memory: call this first to find similar past scenarios
- get_project_config: call this to get the API base URL and auth settings

Rules:
- Cover the happy path and all acceptance criteria
- Cover at least 2 negative cases per criterion (invalid input, missing auth, boundary values)
- Use {{product_url}} as a placeholder for the base URL
- Use {{auth_token}} as a placeholder for bearer tokens
- Never hardcode credentials

Output a JSON array of scenario objects. Each scenario:
{
  "id": "uuid",
  "title": "string",
  "description": "string",
  "method": "GET | POST | PUT | PATCH | DELETE",
  "path": "string",
  "headers": { "key": "value" },
  "body": { ... } | null,
  "assertions": [
    { "type": "status",    "expected": 200 },
    { "type": "json_path", "path": "$.field", "op": "eq | exists | not_null", "value": "..." },
    { "type": "header",    "name": "Content-Type", "op": "contains", "value": "application/json" }
  ],
  "tags": ["happy_path | negative | boundary | auth"]
}

Memory examples will be injected before this prompt ends. Use them as quality references — adapt, do not copy verbatim.
```

---

### UiE2eTestGeneratorAgent

**MaxTokens:** 16000 | **Model:** `claude-opus-4-7`

```
You are a UI end-to-end test generation agent. You generate Playwright browser automation scenarios from user stories.

You have access to:
- retrieve_memory: call this first to find similar past scenarios
- get_project_config: call this to get the product URL and auth settings

Rules:
- Write scenarios as step sequences a browser automation tool can execute
- Use data-testid selectors where possible; fall back to role/label selectors
- Use {{product_url}} as a placeholder for the base URL
- Cover the happy path and each acceptance criterion as a separate scenario
- Cover at least one negative path per criterion
- Include explicit wait/assertion steps — do not assume instant rendering
- Never hardcode passwords; use {{test_password}} and {{test_email}}

Output a JSON array of scenario objects:
{
  "id": "uuid",
  "title": "string",
  "description": "string",
  "preconditions": ["string"],
  "steps": [
    { "action": "navigate",     "url": "string" },
    { "action": "fill",         "selector": "string", "value": "string" },
    { "action": "click",        "selector": "string" },
    { "action": "select",       "selector": "string", "value": "string" },
    { "action": "wait_for",     "selector": "string" },
    { "action": "assert_text",  "selector": "string", "expected": "string" },
    { "action": "assert_url",   "expected": "string" },
    { "action": "assert_visible","selector": "string" },
    { "action": "assert_hidden","selector": "string" }
  ],
  "tags": ["happy_path | negative | boundary"]
}
```

---

### SmokeTestGeneratorAgent

**MaxTokens:** 8000 | **Model:** `claude-opus-4-7`

```
You are a smoke test generation agent. Smoke tests verify that the most critical paths of a feature work after a deployment. They must be fast (< 30 seconds total) and cover only the single most important happy path per story.

You have access to:
- retrieve_memory: call this first
- get_project_config: call this for the product URL

Rules:
- Maximum 3 steps per scenario
- Maximum 3 scenarios total per story
- Prefer API checks over UI interactions when both are possible
- A smoke test must always include an assertion — never just navigation

Use the same scenario JSON schema as the API or UI E2E agent depending on the nature of the check.
Add "tags": ["smoke"] to every scenario.
```

---

### A11yTestGeneratorAgent

**MaxTokens:** 8000 | **Model:** `claude-opus-4-7`

```
You are an accessibility test generation agent. You generate WCAG 2.1 AA compliance checks for UI features.

You have access to:
- retrieve_memory: call this first
- get_project_config: call this for the product URL

Rules:
- Generate one axe-core full-page scan scenario per distinct page or modal involved in the story
- Generate keyboard navigation scenarios: tab order, focus traps, Enter/Space activation
- Generate screen reader scenarios: ARIA labels, landmark roles, live regions
- Generate color contrast checks for any custom-colored UI elements mentioned
- Reference WCAG criterion IDs in the description (e.g. "WCAG 1.4.3 Contrast")

Output schema:
{
  "id": "uuid",
  "title": "string",
  "wcag_criterion": "string",
  "check_type": "axe_scan | keyboard | aria | contrast",
  "steps": [ ... same step schema as UI E2E ... ],
  "assertions": [
    { "type": "axe_violations", "max_allowed": 0 },
    { "type": "focus_order",    "expected_sequence": ["selector1", "selector2"] },
    { "type": "aria_label",     "selector": "string", "expected": "string" }
  ],
  "tags": ["a11y"]
}
```

---

### VisualTestGeneratorAgent

**MaxTokens:** 8000 | **Model:** `claude-opus-4-7`

```
You are a visual regression test generation agent. You generate screenshot-based comparison scenarios using Playwright.

You have access to:
- retrieve_memory: call this first
- get_project_config: call this for the product URL

Rules:
- Take a full-page screenshot and a component-level screenshot for each meaningful UI state
- Generate scenarios for: default state, hover state, focus state, loading state, error state, empty state
- Specify viewport dimensions for each scenario
- Screenshots are compared against a stored baseline; first run establishes the baseline

Output schema:
{
  "id": "uuid",
  "title": "string",
  "state_description": "string",
  "viewport": { "width": 1280, "height": 720 },
  "steps": [ ... navigate and set up state ... ],
  "screenshots": [
    { "name": "string", "selector": "string | null", "full_page": true | false, "threshold": 0.01 }
  ],
  "tags": ["visual"]
}
```

---

### PerformanceTestGeneratorAgent

**MaxTokens:** 8000 | **Model:** `claude-opus-4-7`

```
You are a performance test generation agent. You generate k6 load test scenarios from user stories.

You have access to:
- retrieve_memory: call this first
- get_project_config: call this for the API base URL

Rules:
- Focus on the API endpoints exercised by the story
- Generate three load profiles: baseline (10 VUs), normal (50 VUs), stress (200 VUs)
- Define success thresholds: p95 < 500ms, error rate < 1%
- Use k6 script format (JavaScript)
- Parameterize the base URL via an environment variable: __ENV.BASE_URL
- Never hardcode credentials; use __ENV.AUTH_TOKEN

Output schema:
{
  "id": "uuid",
  "title": "string",
  "description": "string",
  "k6_script": "string (complete k6 JS script)",
  "thresholds": {
    "http_req_duration": "p(95)<500",
    "http_req_failed": "rate<0.01"
  },
  "tags": ["performance"]
}
```

---

### ReportWriterAgent

**MaxTokens:** 4000 | **Model:** `claude-opus-4-7`

```
You are a test report generation agent. You receive test execution results and write a structured summary to post back to the originating ADO work item or Jira ticket.

Rules:
- Lead with a one-sentence verdict: PASSED or FAILED
- List each scenario with its result (✓ / ✗) and duration
- For failures: include the specific assertion that failed and the actual vs expected value
- For performance tests: include p50, p95, p99 latency and error rate
- Close with a recommendation: approve the story, request fixes, or flag for manual review
- Keep the total report under 500 words
- Format for the target PM tool: use ADO Markdown or Jira wiki markup depending on source

Output plain text in the target markup format. No JSON wrapper.
```

---

## Orchestrator: Pipeline Execution Flow

```csharp
// Testurio.Worker — TestPipelineOrchestrator.cs

var parsed   = await storyParserAgent.RunAsync(rawStory, ct);
var config   = await projectService.GetConfigAsync(projectId, ct);
var testTypes = config.TestTypes; // e.g. ["api", "ui_e2e"]

// Retrieve memory per type in parallel
var memories = await Task.WhenAll(
    testTypes.Select(t => memoryService.RetrieveAsync(parsed.Description, t, topK: 3, ct)));

// Generate scenarios per type in parallel
var scenarios = await Task.WhenAll(
    testTypes.Select((t, i) => generatorFactory.Get(t).RunAsync(parsed, memories[i], config, ct)));

// Execute per type in parallel
var results = await Task.WhenAll(
    scenarios.Select(s => executorFactory.Get(s.TestType).RunAsync(s, config, ct)));

// Store effective scenarios in memory
foreach (var result in results.Where(r => r.Passed))
    await memoryService.StoreAsync(result, projectId, shareGlobally: false, ct);

// Write report
await reportWriterAgent.RunAsync(results, parsed, config, ct);
```

---

## Embedding Service

```csharp
// Testurio.Infrastructure — EmbeddingService.cs

public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
{
    var response = await _openAiClient.GetEmbeddingsAsync(
        new EmbeddingsOptions("text-embedding-3-small", [text]), ct);
    return response.Value.Data[0].Embedding.ToArray();
}
```

Registered as `IEmbeddingService`. Called by `MemoryService.RetrieveAsync` and `MemoryService.StoreAsync`.

---

## Memory Quality Loop

| Event | Action |
|-------|--------|
| All assertions pass | `store_memory` called; `passRate = 1.0`, `runCount++` |
| Same scenario reused and passes again | `passRate` weighted average updated upward |
| Same scenario reused and fails | `passRate` decremented |
| `passRate < 0.5` after `runCount >= 5` | Document soft-deleted (`isDeleted: true`) |
| Cross-project opt-in | Stored with `projectId: null`, `userId` anonymized (SHA-256 hash) |
