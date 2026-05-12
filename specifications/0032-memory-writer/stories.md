# User Stories — Memory Writer Service (0032)

## Out of Scope

The following are explicitly **not** part of this feature:

- API endpoint and portal UI for toggling `CrossProjectMemoryOptIn` — the field is introduced at the domain and Cosmos layers; user-facing control is a project-settings concern deferred to a subsequent feature
- Retrieval of cross-project entries (`projectId: null`, hashed `userId`) by MemoryRetrieval (stage 3) — post-MVP
- `passRate` and `runCount` updates on reused memory entries — handled by FeedbackLoop (0031)
- Soft-delete logic — handled by FeedbackLoop (0031)
- Memory entry management UI — post-MVP (feature 0040)
- Deduplication across different stories: dedup applies only within the same `(userId, projectId, testType, storyText)` key

---

## Stories

### US-001: No-Op When Run Is Not All-Pass

**As the** pipeline  
**I want** MemoryWriter to exit immediately when the run is not an all-pass  
**So that** failed or partial runs never pollute the memory store with low-quality scenarios

#### Acceptance Criteria

- [ ] AC-001: `IMemoryWriter.WriteAsync` returns immediately without any embedding call or Cosmos write when any `ApiScenarioResult.Passed` in `executionResult.ApiResults` is `false`.
- [ ] AC-002: `IMemoryWriter.WriteAsync` returns immediately without any embedding call or Cosmos write when any `UiE2eScenarioResult.Passed` in `executionResult.UiE2eResults` is `false`.
- [ ] AC-003: `IMemoryWriter.WriteAsync` returns immediately without any embedding call or Cosmos write when both `executionResult.ApiResults` and `executionResult.UiE2eResults` are empty.

---

### US-002: Generate Story Embedding

**As the** pipeline  
**I want** MemoryWriter to generate a vector embedding of the parsed story text  
**So that** future runs can retrieve semantically similar scenarios as few-shot examples

#### Acceptance Criteria

- [ ] AC-004: On an all-pass run, `IEmbeddingService.EmbedAsync(parsedStory.StoryText, ct)` is called exactly once, regardless of how many scenarios are written.
- [ ] AC-005: The resulting `float32[1536]` vector is stored in `storyEmbedding` on every `TestMemoryEntry` written during that run.

---

### US-003: Upsert Memory Entries Per Scenario with Deduplication

**As the** pipeline  
**I want** each generated scenario from the passing run to be stored in the `TestMemory` container without creating duplicate entries for the same story  
**So that** the memory store grows cleanly and each story retains a single, up-to-date scenario entry per test type

#### Acceptance Criteria

- [ ] AC-006: For each scenario in `generatorResult.ApiScenarios` (when `executionResult.ApiResults` is non-empty), MemoryWriter upserts a `TestMemoryEntry` with `testType = "api"`.
- [ ] AC-007: For each scenario in `generatorResult.UiE2eScenarios` (when `executionResult.UiE2eResults` is non-empty), MemoryWriter upserts a `TestMemoryEntry` with `testType = "ui_e2e"`.
- [ ] AC-008: Each entry's `id` is a deterministic lowercase-hex SHA-256 hash of the concatenation `userId + ":" + (projectId ?? "") + ":" + testType + ":" + storyText`. This makes Cosmos `UpsertItemAsync` naturally insert or replace without a prior read.
- [ ] AC-009: On first write (no existing entry for the deterministic `id`): `passRate = 1.0`, `runCount = 1`, `isDeleted = false`, `lastUsedAt = UtcNow`.
- [ ] AC-010: On a subsequent write (entry already exists — dedup hit): `passRate`, `runCount`, and `isDeleted` are read from the existing document and preserved in the replacement write. `scenarioText`, `storyEmbedding`, and `lastUsedAt` are updated to the values computed in this run.
- [ ] AC-011: `scenarioText` is the scenario object serialised to a compact JSON string (no indentation).
- [ ] AC-012: `storyText` is set to `parsedStory.StoryText`, `userId` to the raw user identifier, and `projectId` to `testRun.ProjectId`.

---

### US-004: Cross-Project Memory Opt-In

**As a** QA lead whose project has `CrossProjectMemoryOptIn` enabled  
**I want** each passing scenario to also be written as an anonymised entry in the shared memory pool  
**So that** the platform can (post-MVP) offer cross-project few-shot examples to other projects

#### Acceptance Criteria

- [ ] AC-013: When `project.CrossProjectMemoryOptIn` is `false` (or the field is absent / defaults to `false`), only the project-scoped entry described in US-003 is written. No additional Cosmos writes occur.
- [ ] AC-014: When `project.CrossProjectMemoryOptIn` is `true`, MemoryWriter writes a second entry per scenario immediately after the project-scoped write. The cross-project entry uses `userId = SHA-256(rawUserId).ToLowerHex()` and `projectId = null`.
- [ ] AC-015: The deterministic `id` for the cross-project entry is computed with the hashed `userId` and empty `projectId` segment: `SHA256(hashedUserId + ":" + "" + ":" + testType + ":" + storyText)`.
- [ ] AC-016: `passRate`, `runCount`, `isDeleted`, and `lastUsedAt` on the cross-project entry follow the same insert/dedup rules as the project-scoped entry (AC-009 and AC-010), evaluated independently against the cross-project entry's own deterministic `id`.
- [ ] AC-017: Cross-project entries are written to the `TestMemory` Cosmos container with the hashed `userId` as the partition key. They are not returned by MemoryRetrieval (stage 3) in this MVP — retrieval of cross-project entries is deferred to post-MVP.

---

### US-005: Non-Throwing Stage 8 Integration

**As the** pipeline  
**I want** MemoryWriter to never throw or propagate exceptions to `TestRunJobProcessor`  
**So that** a transient embedding or Cosmos failure at this final stage does not cause the Service Bus message to be retried or the pipeline run to be marked as an error

#### Acceptance Criteria

- [ ] AC-018: `TestRunJobProcessor` calls `IMemoryWriter.WriteAsync(executionResult, generatorResult, parsedStory, testRun, cancellationToken)` as stage 8, immediately after stage 7 (`IFeedbackLoop.RunAsync`) completes.
- [ ] AC-019: Any exception thrown inside `IMemoryWriter.WriteAsync` is caught by the implementation, logged as a structured warning at `Warning` level (including `runId` and the exception message), and not rethrown. The Service Bus message is not affected.
- [ ] AC-020: After stage 8 completes (with or without an internal exception), `TestRunJobProcessor` marks the job as complete and acknowledges the Service Bus message.
- [ ] AC-021: The `CancellationToken` is forwarded to `IEmbeddingService.EmbedAsync` and to every Cosmos write call inside `WriteAsync` so that pipeline cancellation is respected.
