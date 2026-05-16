# Progress — Agent Router (0026)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-12 |       |
| Plan      | ✅ Complete | 2026-05-12 |       |
| Implement | ✅ Complete | 2026-05-15 |       |
| Review    | ✅ Complete | 2026-05-15 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-15

### Warnings fixed
- `source/Testurio.Pipeline.AgentRouter/AgentRouterService.cs`:96–99 — Removed discard loop that called `_generatorFactory.Create` and threw away results; factory is stage 4's concern, not the router's. Also removed the now-unused `_generatorFactory` field and constructor parameter.
- `source/Testurio.Pipeline.AgentRouter/AgentRouterService.cs`:89 — Changed `await _skipCommentPoster.PostSkipCommentAsync(...)` to `_ = _skipCommentPoster.PostSkipCommentAsync(...)` so the comment post is truly fire-and-forget and does not block `RouteAsync` on the outbound HTTP call.
- `source/Testurio.Pipeline.AgentRouter/SkipCommentPoster.cs`:125 — Added null/empty guard for `AdoTokenSecretUri` before calling `_secretResolver.ResolveAsync`; passing an empty string to Key Vault was semantically invalid. Added `LogAdoCredentialsMissing` log method.
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`:177–183 — `BuildMinimalParsedStory` was setting `AcceptanceCriteria = Array.Empty<string>()`, violating the `ParsedStory` type contract ("contains at least one entry"). Replaced with a placeholder string so the contract is honoured and the limitation is visible in the Claude prompt.
- `source/Testurio.Pipeline.AgentRouter/Testurio.Pipeline.AgentRouter.csproj`:15 — Downgraded `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` from `10.0.7` to `9.0.5` to match the `net9.0` target framework.

### Suggestions fixed
- `source/Testurio.Pipeline.AgentRouter/SkipCommentPoster.cs`:84–94 — Combined two sequential `if (string.IsNullOrEmpty(...))` guards for Jira credentials into a single combined guard to eliminate duplicate log call and dead code.
- `source/Testurio.Pipeline.AgentRouter/DependencyInjection.cs`:33–34 — Replaced double registration (`AddSingleton<AgentRouterService>()` + factory delegate resolving the same instance) with a single `AddSingleton<IAgentRouter, AgentRouterService>()`.
- `tests/Testurio.UnitTests/Pipeline/AgentRouterServiceTests.cs` — Removed `ITestGeneratorFactory` parameter from `CreateSut` and the `ServiceCollection` build since factory is no longer injected into `AgentRouterService`.
- `tests/Testurio.IntegrationTests/Pipeline/AgentRouterIntegrationTests.cs` — Same cleanup as unit tests: removed factory construction and `ServiceCollection` build from `CreateAgentRouterService`.

### Status: Complete

---

## Test Results

_Populated by `/test [####]`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
