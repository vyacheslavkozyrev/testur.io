# Implementation Plan — Cosmos-Backed Prompt Template Management (0047)

## Tasks

### Domain layer

- [x] T001 [Domain] Rewrite `PromptTemplate` record: replace `SystemPrompt` (string), `GeneratorInstruction` (string), `MaxScenarios` (int), and `Version` (string) fields with a unified `Body` (string) field plus `Stage` (string), `Version` (int), `IsActive` (bool), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset); retain `Id` and `TemplateType` (kept as alias for `Stage` for Cosmos partition key compatibility) — `source/Testurio.Core/Models/PromptTemplate.cs`
- [x] T002 [Domain] Remove `PromptTemplate` reference from `GeneratorContext`: replace the `PromptTemplate PromptTemplate` property with a `string SystemPrompt` property (the resolved `Body` string); update the XML doc comment — `source/Testurio.Core/Models/GeneratorContext.cs`
- [x] T003 [Domain] Extend `IPromptTemplateRepository`: add `UpdateAsync(PromptTemplate template, CancellationToken ct) → Task` and `GetAllAsync(CancellationToken ct) → Task<IReadOnlyList<PromptTemplate>>` methods alongside the existing `GetAsync` — `source/Testurio.Core/Interfaces/IPromptTemplateRepository.cs`

### Infrastructure layer

- [x] T004 [Infra] Implement `UpdateAsync` on `PromptTemplateRepository`: full-document replace using `ReplaceItemAsync` keyed by `id = template.Stage`; throw `InvalidOperationException` when Cosmos returns `404` — `source/Testurio.Infrastructure/Cosmos/PromptTemplateRepository.cs`
- [x] T005 [Infra] Implement `GetAllAsync` on `PromptTemplateRepository`: cross-partition query `SELECT * FROM c` against the `PromptTemplates` container; returns all documents regardless of `IsActive` — `source/Testurio.Infrastructure/Cosmos/PromptTemplateRepository.cs`
- [x] T006 [Infra] Create `PromptTemplateService`: wraps `IPromptTemplateRepository` with `IHybridCache`; exposes `GetActiveBodyAsync(string stage, CancellationToken) → Task<string>`; cache key `"prompt-template:{stage}"`, TTL five minutes; throws `InvalidOperationException` (propagated) when document is missing; throws `InvalidOperationException` with message `"PromptTemplate for stage '{stage}' exists but IsActive is false…"` when `IsActive == false`; does not cache a missing or inactive result — `source/Testurio.Infrastructure/Prompt/PromptTemplateService.cs`
- [x] T007 [Infra] Create `IPromptTemplateService` interface in `Testurio.Core`: `GetActiveBodyAsync(string stage, CancellationToken) → Task<string>` and `EvictAsync(string stage, CancellationToken) → Task` (for admin cache eviction after PUT) — `source/Testurio.Core/Interfaces/IPromptTemplateService.cs`
- [x] T008 [Infra] Implement `EvictAsync` on `PromptTemplateService`: calls `IHybridCache.RemoveAsync("prompt-template:{stage}", ct)`; logs `Warning` on failure but does not throw — `source/Testurio.Infrastructure/Prompt/PromptTemplateService.cs`
- [x] T009 [Infra] Register `IPromptTemplateService` → `PromptTemplateService` (singleton) in shared DI; update the existing `IPromptTemplateRepository` comment to note 0047 scope — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T010 [Infra] Update `PromptTemplateSeeder`: rewrite the two existing seed documents (`api_test_generator`, `ui_e2e_test_generator`) to use the new unified schema (`Body`, `Stage`, `Version: 1`, `IsActive: true`, `CreatedAt`, `UpdatedAt`); merge `SystemPrompt` + `GeneratorInstruction` into a single `Body` string; remove `MaxScenarios` field; add three new seed documents for `story_parser`, `agent_router`, and `report_writer` using the verbatim `const string SystemPrompt` text extracted from `AiStoryConverter`, `StoryClassifier`, and `ReportWriter` respectively — `source/Testurio.Infrastructure/Seeding/PromptTemplateSeeder.cs`

### Application layer — Pipeline stages

- [ ] T011 [App] Update `PromptAssemblyService`: change `Assemble` signature to `Assemble(string systemPrompt, GeneratorContext context, out string outSystemPrompt)` → simplify to `Assemble(GeneratorContext context)` returning `(string systemPrompt, string userPrompt)` tuple; read `context.SystemPrompt` (the resolved `Body` string) as the system prompt; remove `{{maxScenarios}}` substitution logic; remove all references to `PromptTemplate` fields — `source/Testurio.Pipeline.Generators/Services/PromptAssemblyService.cs`
- [ ] T012 [App] Update `AiStoryConverter`: inject `IPromptTemplateService`; replace the `const string SystemPrompt` field with a call to `IPromptTemplateService.GetActiveBodyAsync("story_parser", ct)` at the start of `ConvertAsync` — `source/Testurio.Pipeline.StoryParser/AiStoryConverter.cs`
- [ ] T013 [App] Update `StoryClassifier`: inject `IPromptTemplateService`; replace the `const string SystemPrompt` field with a call to `IPromptTemplateService.GetActiveBodyAsync("agent_router", ct)` at the start of `ClassifyAsync` — `source/Testurio.Pipeline.AgentRouter/StoryClassifier.cs`
- [ ] T014 [App] Update `ApiTestGeneratorAgent`: replace the `PromptAssemblyService` call pattern to use the updated `Assemble` signature; construct `GeneratorContext.SystemPrompt` from `IPromptTemplateService.GetActiveBodyAsync("api_test_generator", ct)` in `TestRunJobProcessor` (T016) rather than directly in the agent — `source/Testurio.Pipeline.Generators/ApiTestGeneratorAgent.cs`
- [ ] T015 [App] Update `UiE2eTestGeneratorAgent`: same pattern as T014 for the `"ui_e2e_test_generator"` stage — `source/Testurio.Pipeline.Generators/UiE2eTestGeneratorAgent.cs`
- [ ] T016 [App] Update `TestRunJobProcessor.ExecutePipelineAsync`: remove direct `IPromptTemplateRepository` injection; inject `IPromptTemplateService` instead; resolve `api_test_generator` and `ui_e2e_test_generator` body strings via `IPromptTemplateService.GetActiveBodyAsync` before constructing `GeneratorContext` instances; pass each resolved body as `GeneratorContext.SystemPrompt` — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T017 [App] Update `ReportWriter`: inject `IPromptTemplateService`; replace the `const string SystemPrompt` field with a call to `IPromptTemplateService.GetActiveBodyAsync("report_writer", ct)` at the start of `GenerateReportContentAsync` — `source/Testurio.Pipeline.ReportWriter/ReportWriter.cs`

### DI wiring for pipeline projects

- [ ] T018 [Infra] Update `Testurio.Pipeline.StoryParser/DependencyInjection.cs`: register `IPromptTemplateService` pass-through (the singleton is already registered by `Testurio.Infrastructure`); ensure `AiStoryConverter` can resolve `IPromptTemplateService` from the container — `source/Testurio.Pipeline.StoryParser/DependencyInjection.cs`
- [ ] T019 [Infra] Update `Testurio.Pipeline.AgentRouter/DependencyInjection.cs`: ensure `StoryClassifier` can resolve `IPromptTemplateService` — `source/Testurio.Pipeline.AgentRouter/DependencyInjection.cs`
- [ ] T020 [Infra] Update `Testurio.Pipeline.ReportWriter/DependencyInjection.cs`: ensure `ReportWriter` can resolve `IPromptTemplateService` — `source/Testurio.Pipeline.ReportWriter/DependencyInjection.cs`
- [ ] T021 [Infra] Update `Testurio.Worker/DependencyInjection.cs`: replace `IPromptTemplateRepository` with `IPromptTemplateService` in the `TestRunJobProcessor` constructor call; ensure `IHybridCache` is registered (add `services.AddHybridCache()` if not already present) — `source/Testurio.Worker/DependencyInjection.cs`

### API layer — Admin endpoints

- [ ] T022 [App] Create `PromptTemplateDto` and `UpdatePromptTemplateRequest` DTOs — `source/Testurio.Api/DTOs/PromptTemplateDtos.cs`
- [ ] T023 [API] Create `AdminPromptTemplateEndpoints`: map `GET /v1/admin/prompt-templates/{stage}` (returns `PromptTemplateDto` or `404`; validates `stage` against the five known keys); map `PUT /v1/admin/prompt-templates/{stage}` (validates body, calls service, evicts cache, returns updated `PromptTemplateDto`); both endpoints require `admin` role policy — `source/Testurio.Api/Endpoints/AdminPromptTemplateEndpoints.cs`
- [ ] T024 [App] Create `IPromptTemplateAdminService` and `PromptTemplateAdminService`: `GetAsync(stage, ct)` reads via `IPromptTemplateRepository.GetAllAsync` and filters by stage; `UpdateAsync(stage, body, ct)` reads current document, increments `Version`, sets `Body`, `UpdatedAt`, `IsActive = true`, writes via `IPromptTemplateRepository.UpdateAsync`, then calls `IPromptTemplateService.EvictAsync` — `source/Testurio.Api/Services/PromptTemplateAdminService.cs`
- [ ] T025 [API] Register `PromptTemplateAdminService` and map admin endpoint group in `Testurio.Api`; add `admin` role authorization policy if not already defined — `source/Testurio.Api/Program.cs` (or equivalent startup file)

### Tests

- [ ] T026 [Test] Unit tests for `PromptTemplateService`: `GetActiveBodyAsync` returns cached value on second call; throws `InvalidOperationException` when document missing; throws `InvalidOperationException` when `IsActive == false`; does not cache error state — `tests/Testurio.UnitTests/Services/PromptTemplateServiceTests.cs`
- [ ] T027 [Test] Unit tests for `PromptTemplateAdminService`: `GetAsync` returns `PromptTemplateDto` for valid stage; returns null for unknown stage; `UpdateAsync` increments version and evicts cache — `tests/Testurio.UnitTests/Services/PromptTemplateAdminServiceTests.cs`
- [ ] T028 [Test] Unit tests for `PromptAssemblyService`: verify `SystemPrompt` is correctly passed through; verify memory examples section present/absent; verify custom prompt section present/absent; verify `{{maxScenarios}}` placeholder is no longer substituted (i.e. passes through literally) — `tests/Testurio.UnitTests/Services/PromptAssemblyServiceTests.cs`
- [ ] T029 [Test] Integration tests for admin endpoints: `GET /v1/admin/prompt-templates/report_writer` returns `200` with correct shape; `GET` with unknown stage returns `400`; `PUT` returns `200` with incremented version; unauthenticated request returns `401`; request without `admin` role returns `403` — `tests/Testurio.IntegrationTests/Controllers/AdminPromptTemplateControllerTests.cs`
- [ ] T030 [Test] E2E tests: n/a — admin endpoints are operator tooling with no public UI; mark as skipped — `source/Testurio.Web/e2e/prompt-template-admin.spec.ts` _(skipped)_

---

## Rationale

### Layer order

**Domain first (T001–T003).** The `PromptTemplate` model change (T001) is the single most impactful change in this feature — every other task either reads from or writes to the new schema. `GeneratorContext` (T002) must be updated immediately after because it references `PromptTemplate` directly and every downstream task that constructs a `GeneratorContext` depends on the new shape. `IPromptTemplateRepository` extension (T003) defines the contract that the infrastructure implementation must satisfy; it must exist before the concrete class can be changed.

**Infrastructure caching layer (T004–T010) before application layer (T011–T021).** `PromptTemplateService` (T006–T008) is the new caching wrapper that all pipeline stages will call. It cannot be implemented until the updated `PromptTemplate` model (T001) and extended `IPromptTemplateRepository` (T003–T005) are in place. DI registration (T009) must precede the Worker and pipeline stage changes (T018–T021) so the container can resolve `IPromptTemplateService`. The seeder (T010) is placed here because it writes documents in the new schema — it depends on the final `PromptTemplate` shape being stable.

**Pipeline stage changes (T011–T021) follow the infrastructure layer.** Each stage change injects `IPromptTemplateService` and removes its `const string SystemPrompt`; this is only possible once the service interface (T007) and its registration (T009) are in place. `PromptAssemblyService` (T011) is first because `ApiTestGeneratorAgent` (T014) and `UiE2eTestGeneratorAgent` (T015) both call it; the generator agents must compile after their dependency is updated. `TestRunJobProcessor` (T016) is last in this group because it orchestrates all stage invocations and depends on all of them compiling correctly.

**Admin API (T022–T025) follows the infrastructure layer** but is independent of the pipeline stage changes — the admin service reads and writes templates without invoking any pipeline stage. It is ordered after T009 (DI registration) so `IPromptTemplateRepository` and `IPromptTemplateService` are available for injection. The endpoint registration (T025) is last in this group because it depends on the service (T024) and the DTOs (T022).

**Tests last (T026–T030).** Following the `[Test]` tag rule — all implementation layers must be stable before test classes are written. Unit tests (T026–T028) are ordered before integration tests (T029) because they exercise individual classes in isolation and catch regressions faster.

### Cross-feature dependencies

- **Feature 0028 (Test Generator Agents):** The `PromptTemplate` model, `PromptTemplateRepository`, `PromptTemplateSeeder`, `IPromptTemplateRepository`, and the generator-agent fields (`SystemPrompt`, `GeneratorInstruction`, `MaxScenarios`) all originate in feature 0028. This feature is a direct continuation that extends those artefacts rather than replacing them from scratch. The two existing seed documents are migrated to the new schema — not deleted — ensuring no data loss in environments that have already run the feature 0028 seeder. The `PromptTemplateSeeder.SeedAsync` idempotency check (skip if document already exists) protects existing documents.

- **Feature 0046 (Plan Enforcement):** `TestRunJobProcessor` was updated in feature 0046 to inject `IPlanEnforcementService`. This feature further modifies `TestRunJobProcessor` (T016) to replace `IPromptTemplateRepository` with `IPromptTemplateService`. Both changes must be committed in the same file — they are additive and do not conflict.

- **Feature 0030 (ReportWriter):** `ReportWriter` was implemented in feature 0030 with a hardcoded `const string SystemPrompt`. This feature removes that constant and replaces it with a Cosmos-backed lookup. The `IReportWriter.WriteAsync` signature is unchanged — only the internal implementation changes.

- **Feature 0025 (StoryParser):** `AiStoryConverter` was implemented in feature 0025 with a hardcoded `const string SystemPrompt`. This feature injects `IPromptTemplateService` into `AiStoryConverter`. The `IStoryParser.ParseAsync` interface signature is unchanged.

- **Feature 0026 (AgentRouter):** `StoryClassifier` was implemented in feature 0026 with a hardcoded `const string SystemPrompt`. This feature injects `IPromptTemplateService` into `StoryClassifier`. The `IAgentRouter.RouteAsync` interface signature is unchanged.

### Architectural decisions

**`IPromptTemplateService` lives in `Testurio.Core` (not `Testurio.Infrastructure`).** Pipeline projects (`Testurio.Pipeline.StoryParser`, `Testurio.Pipeline.AgentRouter`, etc.) depend on `Testurio.Core` but not on `Testurio.Infrastructure`. Placing `IPromptTemplateService` in `Testurio.Core` allows pipeline stage classes to inject the caching abstraction without taking a direct infrastructure dependency. The concrete `PromptTemplateService` with `IHybridCache` remains in `Testurio.Infrastructure`.

**`IHybridCache` rather than `IMemoryCache` or `IDistributedCache`.** The backend rules specify `IHybridCache` as the preferred caching abstraction (replacing both `IMemoryCache` and `IDistributedCache`). Its `GetOrCreateAsync` pattern is safe under concurrent pipeline runs — only one Cosmos read fires when the cache entry is absent.

**Cache eviction on admin PUT (AC-026).** After a successful `PUT /v1/admin/prompt-templates/{stage}`, the admin service explicitly evicts the corresponding `IHybridCache` entry via `IPromptTemplateService.EvictAsync`. This means the updated prompt body is available on the next pipeline run without waiting for the five-minute TTL. If eviction fails (e.g. distributed cache temporarily unavailable), the request still succeeds — the old prompt will expire naturally within five minutes.

**Generator prompt `Body` is a single string.** The previous schema split the generator prompt across `SystemPrompt` (passed to the Claude `system` field) and `GeneratorInstruction` (appended to the user turn). The new unified `Body` field is treated as the system prompt and passed directly to `ILlmGenerationClient.CompleteAsync` as the `systemPrompt` parameter — consistent with how `AiStoryConverter`, `StoryClassifier`, and `ReportWriter` already use a single system prompt string. Operators editing the template body have full control over the entire system prompt text. The `PromptAssemblyService` continues to assemble the multi-layer user-turn prompt (memory examples, custom prompt, testing strategy, parsed story) independently of the system prompt.

**Admin endpoint uses `IPromptTemplateAdminService` rather than calling `IPromptTemplateRepository` directly from the endpoint handler.** This keeps the endpoint handlers thin (matching the API style rules) and isolates the version-increment and cache-eviction logic in a testable service class.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, service implementations, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators, Worker pipeline steps |
| `[API]` | Minimal API endpoints, middleware — `Testurio.Api` |
| `[Config]` | App configuration, seed data, constants |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n keys |
| `[Test]` | Unit, integration, and frontend component test files |
