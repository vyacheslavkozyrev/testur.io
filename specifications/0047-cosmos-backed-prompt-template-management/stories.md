# User Stories — Cosmos-Backed Prompt Template Management (0047)

## Out of Scope

The following are explicitly **not** part of this feature:

- Moving per-project custom prompts (feature 0008) into Cosmos — those are user-authored and already stored in the `Project` document
- Versioned prompt history or rollback UI — the admin endpoint replaces the active template in place; no audit log is required at this stage
- Access control UI for the admin endpoint — authorisation is enforced by a single `admin` role claim; no admin portal page is built
- A/B testing or staged rollout of prompt templates — a single active document per stage is the only supported state
- Prompt template management for post-MVP pipeline stages (smoke, a11y, visual, performance)
- Automatic invalidation of the `IHybridCache` entry when a template is updated via the admin endpoint — the five-minute TTL expiry is the only invalidation mechanism
- Migrating generator-agent prompt templates that are _already_ stored in Cosmos (they exist under `PromptTemplate` with a narrower schema) — this feature **extends** the existing `PromptTemplate` model to cover all pipeline stages and unifies the schema; the existing seed documents are updated, not replaced

---

## Stories

### US-001: PromptTemplate model is extended to cover all pipeline stages

**As the** Testurio pipeline
**I want to** load the full prompt for every stage (StoryParser, AgentRouter, ApiTestGenerator, UiE2eTestGenerator, ReportWriter) from a single `PromptTemplate` document schema in Cosmos
**So that** all stage prompts follow the same storage and retrieval contract and no pipeline stage contains a hardcoded prompt string

#### Acceptance Criteria

- [ ] AC-001: `PromptTemplate` record in `Testurio.Core/Models/PromptTemplate.cs` is extended with the following new fields: `Stage` (string — the stage key, e.g. `"story_parser"`), `Version` (int — monotonically incrementing integer, replacing the current `string Version`), `Body` (string — the full prompt text, replacing the current multi-field layout of `SystemPrompt` + `GeneratorInstruction` + `MaxScenarios`), `IsActive` (bool), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset)
- [ ] AC-002: The `Stage` field doubles as the Cosmos document `id` (same value) so a single point-read suffices; the Cosmos partition key path remains `/templateType` (kept as an alias for `Stage` for backward compatibility)
- [ ] AC-003: Valid `Stage` key values for MVP pipeline stages are: `"story_parser"`, `"agent_router"`, `"api_test_generator"`, `"ui_e2e_test_generator"`, `"report_writer"`
- [ ] AC-004: The existing generator-only fields (`SystemPrompt`, `GeneratorInstruction`, `MaxScenarios`) are removed from `PromptTemplate`; callers that previously read those fields are updated to use the unified `Body` field
- [ ] AC-005: `IPromptTemplateRepository.GetAsync(string stage, CancellationToken)` contract is unchanged; the concrete `PromptTemplateRepository` implementation continues to perform a point-read by `id = stage`; no interface change is required
- [ ] AC-006: `PromptTemplateRepository.GetAsync` throws `InvalidOperationException` (same as today) when no document with the given `stage` key exists — the pipeline must not continue without a resolved prompt

---

### US-002: Each pipeline stage resolves its prompt from Cosmos at startup via IHybridCache

**As the** Testurio pipeline
**I want to** retrieve the active `PromptTemplate` document for my stage from Cosmos at the start of each pipeline run, with results cached for five minutes
**So that** prompt changes made via the admin endpoint take effect within five minutes without requiring a code deployment or worker restart

#### Acceptance Criteria

- [ ] AC-007: A `PromptTemplateService` class (or equivalent caching wrapper) in `Testurio.Infrastructure` wraps `IPromptTemplateRepository` with `IHybridCache`; it exposes `GetActiveBodyAsync(string stage, CancellationToken) → Task<string>`
- [ ] AC-008: The cache key for each stage is `"prompt-template:{stage}"` (e.g. `"prompt-template:story_parser"`); TTL is exactly five minutes
- [ ] AC-009: When the cached value is absent, `PromptTemplateService` calls `IPromptTemplateRepository.GetAsync(stage, ct)` and stores the result; subsequent calls within the five-minute window return the cached value without hitting Cosmos
- [ ] AC-010: `StoryParserService` (Stage 1) replaces its hardcoded `const string SystemPrompt` with a call to `PromptTemplateService.GetActiveBodyAsync("story_parser", ct)` at the start of each `ParseAsync` invocation (specifically in `AiStoryConverter.ConvertAsync`)
- [ ] AC-011: `StoryClassifier` (Stage 2 — AgentRouter) replaces its hardcoded `const string SystemPrompt` with a call to `PromptTemplateService.GetActiveBodyAsync("agent_router", ct)` at the start of each `ClassifyAsync` invocation
- [ ] AC-012: `ApiTestGeneratorAgent` (Stage 4) replaces its use of the generator-template fields (`SystemPrompt`, `GeneratorInstruction`) with `PromptTemplateService.GetActiveBodyAsync("api_test_generator", ct)`; the `PromptAssemblyService` is updated accordingly
- [ ] AC-013: `UiE2eTestGeneratorAgent` (Stage 4) replaces its use of the generator-template fields with `PromptTemplateService.GetActiveBodyAsync("ui_e2e_test_generator", ct)`
- [ ] AC-014: `ReportWriter` (Stage 6) replaces its hardcoded `const string SystemPrompt` with a call to `PromptTemplateService.GetActiveBodyAsync("report_writer", ct)` at the start of each `WriteAsync` invocation
- [ ] AC-015: If `PromptTemplateService.GetActiveBodyAsync` throws (Cosmos unavailable, document missing), the exception propagates to the caller without being swallowed; the pipeline stage fails and the `TestRunJobProcessor` dead-letters the message (existing behaviour for `InvalidOperationException`)

---

### US-003: If no active template is found, the pipeline fails immediately with a clear exception

**As the** Testurio operations team
**I want to** see an immediate startup exception when a pipeline stage cannot resolve its active prompt template
**So that** misconfiguration is caught at the first job attempt rather than silently producing empty or wrong prompts

#### Acceptance Criteria

- [ ] AC-016: When `IPromptTemplateRepository.GetAsync` returns a document with `IsActive == false`, `PromptTemplateService.GetActiveBodyAsync` throws `InvalidOperationException` with message `"PromptTemplate for stage '{stage}' exists but IsActive is false. Activate a template before starting the worker."`
- [ ] AC-017: When `IPromptTemplateRepository.GetAsync` throws `InvalidOperationException` (document not found), the exception is propagated unchanged by `PromptTemplateService`; no secondary fallback or default prompt is substituted
- [ ] AC-018: The `InvalidOperationException` propagates from the pipeline stage through `TestRunJobProcessor.ExecutePipelineAsync`, which dead-letters the Service Bus message with reason `"InvalidOperationException"` — consistent with the existing error handling contract already in place for this exception type
- [ ] AC-019: `PromptTemplateService` does NOT cache a missing or inactive template result — a subsequent call after the document is corrected in Cosmos must retrieve the live document rather than a cached error state

---

### US-004: Admin operators can read and replace the active prompt body via an API endpoint

**As an** authorised admin operator
**I want to** read and replace the active prompt body for any pipeline stage via a REST endpoint
**So that** I can tune, fix, or extend any pipeline prompt without a code deployment

#### Acceptance Criteria

- [ ] AC-020: A `GET /v1/admin/prompt-templates/{stage}` endpoint returns a `PromptTemplateDto` with fields: `stage` (string), `version` (int), `body` (string), `isActive` (bool), `createdAt` (ISO 8601), `updatedAt` (ISO 8601)
- [ ] AC-021: `GET /v1/admin/prompt-templates/{stage}` returns `404 Not Found` with a `ProblemDetails` body when no document with the given `stage` key exists in the container
- [ ] AC-022: A `PUT /v1/admin/prompt-templates/{stage}` endpoint accepts a `UpdatePromptTemplateRequest` body with a single required field `body` (non-empty string); it updates the active document's `body` field, increments `version` by 1, sets `updatedAt` to the current UTC time, and sets `isActive` to `true`; it returns `200 OK` with the updated `PromptTemplateDto`
- [ ] AC-023: `PUT /v1/admin/prompt-templates/{stage}` returns `404 Not Found` when the `stage` document does not exist; creating a new template document from the endpoint is not supported — only updates to existing documents are permitted
- [ ] AC-024: Both endpoints require the caller to hold the `admin` role claim in their Azure AD B2C JWT; requests without the claim return `403 Forbidden`
- [ ] AC-025: The `{stage}` route parameter is validated against the known valid stage keys (`"story_parser"`, `"agent_router"`, `"api_test_generator"`, `"ui_e2e_test_generator"`, `"report_writer"`); any other value returns `400 Bad Request` with a `ProblemDetails` body before the Cosmos read is attempted
- [ ] AC-026: After a successful `PUT`, the `IHybridCache` entry for `"prompt-template:{stage}"` is explicitly evicted so the updated body is served to the next pipeline run without waiting for the five-minute TTL; if eviction fails it is logged as a warning but does not fail the request

---

### US-005: Cosmos seed data includes active PromptTemplate documents for all five pipeline stages

**As the** Testurio operations team
**I want to** start the worker in any environment and have all five pipeline stage prompts available in Cosmos without manual intervention
**So that** new environment provisioning and CI pipeline runs work out of the box

#### Acceptance Criteria

- [ ] AC-027: `CosmosDbInitializer` seeds one `PromptTemplate` document per pipeline stage (five total) into the `PromptTemplates` container when the document does not already exist (upsert with `if-not-exists` semantics so existing customised documents are never overwritten)
- [ ] AC-028: Each seeded document has `IsActive: true`, `Version: 1`, `CreatedAt` and `UpdatedAt` set to the seed run timestamp, and a `Body` containing the full prompt text previously hardcoded in the corresponding pipeline stage class
- [ ] AC-029: The two existing generator-agent seed documents (`"api_test_generator"`, `"ui_e2e_test_generator"`) are migrated to the new unified `PromptTemplate` schema; the `SystemPrompt` and `GeneratorInstruction` fields are merged into a single `Body` field; `MaxScenarios` is removed from the document (the value is now embedded in the `Body` text itself if needed)
- [ ] AC-030: Seeded `Body` values for `"story_parser"`, `"agent_router"`, and `"report_writer"` are the verbatim text of the `const string SystemPrompt` fields extracted from their respective stage classes at the time of this feature's implementation
- [ ] AC-031: The `PromptTemplates` Cosmos container definition in `CosmosDbInitializer` retains its existing partition key path `/templateType`; the new `Stage` field is written to the same JSON key as `templateType` to maintain point-read compatibility

---

### US-006: IPromptTemplateRepository is extended to support admin write operations

**As the** admin endpoint
**I want to** update a `PromptTemplate` document in Cosmos and evict the cache entry
**So that** operators can push prompt changes with immediate effect on the next uncached pipeline run

#### Acceptance Criteria

- [ ] AC-032: `IPromptTemplateRepository` gains a `UpdateAsync(PromptTemplate template, CancellationToken ct) → Task` method that performs a full-document replace (not a patch) in Cosmos
- [ ] AC-033: `PromptTemplateRepository.UpdateAsync` uses the document `id` (= `stage`) and `templateType` (= `stage`) as the point-write key; it throws `InvalidOperationException` when the document does not exist (treating a missing document as a configuration error, not a 404 to swallow)
- [ ] AC-034: `IPromptTemplateRepository` gains a `GetAllAsync(CancellationToken ct) → Task<IReadOnlyList<PromptTemplate>>` method (admin read) that returns all documents in the container regardless of `IsActive` status, for use by the `GET /v1/admin/prompt-templates/{stage}` endpoint
- [ ] AC-035: Both `UpdateAsync` and `GetAllAsync` are admin-path only — they are not called from any pipeline stage

---

### US-007: PromptAssemblyService is updated to use the unified Body field

**As the** ApiTestGeneratorAgent and UiE2eTestGeneratorAgent
**I want to** receive the complete prompt body as a single string from PromptTemplateService
**So that** the generator agents no longer need to differentiate between `SystemPrompt` and `GeneratorInstruction` fields

#### Acceptance Criteria

- [ ] AC-036: `PromptAssemblyService` in `Testurio.Pipeline.Generators/Services/` is updated to accept a `systemPrompt` string parameter (the resolved `Body`) instead of reading `PromptTemplate.SystemPrompt` and `PromptTemplate.GeneratorInstruction` separately
- [ ] AC-037: The `{{maxScenarios}}` placeholder substitution logic in `PromptAssemblyService` is removed — the seeded `Body` for generator agents embeds the max-scenarios constraint as literal text; if dynamic injection of `MaxScenarios` is needed in the future it is the responsibility of the admin who edits the template body
- [ ] AC-038: `GeneratorContext` no longer carries a `PromptTemplate` field; instead, the resolved `Body` string is passed directly to `PromptAssemblyService.Assemble(string systemPrompt, GeneratorContext context)`
- [ ] AC-039: `TestRunJobProcessor` resolves generator prompt bodies via `PromptTemplateService.GetActiveBodyAsync` once per stage key before constructing `GeneratorContext` instances, replacing the existing `IPromptTemplateRepository` direct call pattern

---

## Negative Paths & Edge Cases

- A `PUT` request with an empty `body` string returns `400 Bad Request` — the body field is required and non-empty
- A `GET` or `PUT` to an unknown `stage` key (e.g. `/v1/admin/prompt-templates/unknown_stage`) returns `400 Bad Request` — not a `404`
- If the worker starts and a required stage document is missing from Cosmos (seeder was not run), the first pipeline job for that stage will throw `InvalidOperationException` and be dead-lettered; the worker itself does not crash on startup
- Concurrent admin `PUT` calls for the same stage are handled by Cosmos optimistic concurrency (ETag); a lost-update race is acceptable at this scale — the last writer wins
