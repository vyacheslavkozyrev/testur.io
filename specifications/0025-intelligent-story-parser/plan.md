# Implementation Plan — Intelligent Story Parser (0025)

## Tasks

- [x] T001 [Domain] Create `WorkItem` model (PM-tool-agnostic input to the parser) — `source/Testurio.Core/Models/WorkItem.cs`
- [x] T002 [Domain] Create `ParsedStory` immutable record (output contract shared with all downstream stages) — `source/Testurio.Core/Models/ParsedStory.cs`
- [x] T003 [Domain] Create `StoryParserException` typed exception — `source/Testurio.Core/Exceptions/StoryParserException.cs`
- [x] T004 [Domain] Add `IStoryParser` interface (`ParseAsync(WorkItem, CancellationToken) → Task<ParsedStory>`) — `source/Testurio.Core/Interfaces/IStoryParser.cs`
- [x] T005 [Domain] Extend `TestRun` entity with `ParserMode` enum field (`direct` | `ai_converted`) — `source/Testurio.Core/Entities/TestRun.cs`
- [x] T006 [Infra] Add `parserMode` field to `TestRunRepository` Cosmos write path — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`
- [x] T007 [Infra] Update DI registration to expose `AnthropicClient` singleton for pipeline projects — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T008 [App] Implement `TemplateChecker` (rule-based: validates title, description, and at least one AC are non-empty) — `source/Testurio.Pipeline.StoryParser/TemplateChecker.cs`
- [ ] T009 [App] Implement `DirectParser` (heuristic extraction of entities, actions, edge_cases from raw text; returns `ParsedStory`) — `source/Testurio.Pipeline.StoryParser/DirectParser.cs`
- [ ] T010 [App] Implement `AiStoryConverter` (calls Claude `claude-opus-4-7` with adaptive thinking; deserialises and validates response against `ParsedStory` schema) — `source/Testurio.Pipeline.StoryParser/AiStoryConverter.cs`
- [ ] T011 [App] Implement `PmToolCommentPoster` (posts warning comment to ADO or Jira ticket asynchronously based on run context; fire-and-forget with error swallowing) — `source/Testurio.Pipeline.StoryParser/PmToolCommentPoster.cs`
- [ ] T012 [App] Implement `StoryParserService` (orchestrates `TemplateChecker` → `DirectParser` or `AiStoryConverter` + `PmToolCommentPoster`; implements `IStoryParser`) — `source/Testurio.Pipeline.StoryParser/StoryParserService.cs`
- [ ] T013 [Config] Register `StoryParserService` as `IStoryParser` and its dependencies in DI — `source/Testurio.Pipeline.StoryParser/DependencyInjection.cs`
- [ ] T014 [Worker] Inject `IStoryParser` into `TestRunJobProcessor` and invoke `ParseAsync` as the first pipeline stage; update `TestRun.ParserMode` after parse; propagate `StoryParserException` to run-failure handler — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T015 [Test] Unit tests for `TemplateChecker` (all three missing-field variants + all-present happy path) — `tests/Testurio.UnitTests/Pipeline/TemplateCheckerTests.cs`
- [ ] T016 [Test] Unit tests for `DirectParser` (field extraction, empty arrays when not detected, no-Claude-call assertion) — `tests/Testurio.UnitTests/Pipeline/DirectParserTests.cs`
- [ ] T017 [Test] Unit tests for `AiStoryConverter` (valid response → schema passes; malformed response → `StoryParserException`; Claude API error → `StoryParserException`) — `tests/Testurio.UnitTests/Pipeline/AiStoryConverterTests.cs`
- [ ] T018 [Test] Unit tests for `StoryParserService` (conformant story → direct path, no AI call, no comment; non-conformant → AI path, comment posted; AI failure → `StoryParserException` thrown; comment-post failure → pipeline continues) — `tests/Testurio.UnitTests/Pipeline/StoryParserServiceTests.cs`
- [ ] T019 [Test] Integration tests for the full parse stage via `TestRunJobProcessor` (conformant story end-to-end; non-conformant story end-to-end with mocked Anthropic and PM tool clients) — `tests/Testurio.IntegrationTests/Pipeline/StoryParserIntegrationTests.cs`

## Rationale

**Domain types before all other layers.** `WorkItem` (T001) and `ParsedStory` (T002) are the input and output contracts of the entire stage. Every class in the pipeline project, every worker processor, and every downstream stage (0026–0032) depends on them. Defining them first — before any infrastructure or application code — ensures a stable, dependency-inversion–clean contract that cannot be broken by later implementation choices. `StoryParserException` (T003) and `IStoryParser` (T004) close the domain layer by giving callers a typed failure channel and a clean abstraction over the implementation.

**`TestRun` extension before infrastructure.** The `ParserMode` enum field (T005) is added to the existing `TestRun` entity from feature 0001. Because `TestRunRepository` (T006) serialises and deserialises this entity, the domain change must precede the infrastructure write-path update. Extending the existing entity (rather than creating a new one) avoids a schema break — Cosmos DB's schema-less model means adding a field is additive and backwards-compatible.

**Infrastructure DI update before pipeline project.** `AnthropicClient` (T007) is registered as a singleton in `Testurio.Infrastructure` so the pipeline project can resolve it without taking a direct dependency on infrastructure initialisation. This keeps `Testurio.Pipeline.StoryParser` infrastructure-ignorant — it only depends on `Testurio.Core` and the Anthropic SDK.

**Internal pipeline ordering: checker → parsers → orchestrator.** `TemplateChecker` (T008) is the simplest component and has no dependencies inside the pipeline project; it is implemented first. `DirectParser` (T009) depends on the `ParsedStory` shape defined in T002 but not on `TemplateChecker` — it can be developed in parallel, but is listed sequentially for clarity. `AiStoryConverter` (T010) has the most external dependencies (Anthropic SDK, `ParsedStory` schema validation) and is implemented after the simpler parsers are stable. `PmToolCommentPoster` (T011) is deliberately decoupled from the parse result — it only needs the run context (PM tool type, ticket reference) and is therefore buildable independently of T008–T010. `StoryParserService` (T012) is the last class to implement because it is the orchestrator that wires all four components together; it cannot be written until all its dependencies exist.

**DI registration before worker integration.** `DependencyInjection.cs` (T013) must be registered before `TestRunJobProcessor` (T014) attempts to resolve `IStoryParser`. The worker is modified last, after the full pipeline stage is stable, to minimise risk of destabilising the queue dispatch logic introduced in feature 0001.

**Cross-feature dependencies.** This feature establishes `WorkItem`, `ParsedStory`, and `IStoryParser` — the shared input/output contract consumed by features 0026 (AgentRouter), 0027 (MemoryRetrieval), 0028 (Generators), and all subsequent pipeline stages. None of those features should begin implementation until T001–T004 are merged and stable. The `StoryParserPlugin` introduced in feature 0002 (POC) becomes redundant once this feature is implemented; a follow-up task in the 0025 implementation notes should mark it for removal.

**No UI tasks.** This feature is a pure backend pipeline stage. The parser mode (`direct` | `ai_converted`) is surfaced in run history via the existing `TestRun` record — no new portal pages or API endpoints are required.

**Tests last, per project QA rules.** Unit tests (T015–T018) are written after each component is complete, covering all acceptance criteria paths. Integration tests (T019) are written last because they exercise the full stage through the `TestRunJobProcessor`, which is only wired in T014. Tests use mocked Anthropic and PM tool clients so no live external calls are made in CI.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Cosmos DB repositories, Service Bus clients, DI registration — `Testurio.Infrastructure` |
| `[App]` | Services, API clients, DTOs — `Testurio.Api` or pipeline projects |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Worker]` | Job processors, queue managers, pipeline steps — `Testurio.Worker` |
| `[Plugin]` | Semantic Kernel plugins — `Testurio.Plugins` |
| `[Config]` | DI registration, app configuration, environment settings — any project |
| `[UI]` | Next.js pages, components, API clients, hooks — `Testurio.Web` |
| `[Test]` | Unit and integration test files — `tests/` |
