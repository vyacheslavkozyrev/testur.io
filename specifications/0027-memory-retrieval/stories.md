# User Stories — Memory Retrieval Service (0027)

## Out of Scope

The following are explicitly **not** part of this feature:

- Cross-project memory retrieval — retrieval is scoped to `userId + projectId` only (cross-project sharing is post-MVP, feature 0039)
- Filtering retrieved scenarios by test type — the search returns the top-3 most similar entries at project scope regardless of test type
- Writing or updating memory entries — covered by 0032 (MemoryWriter) and 0031 (FeedbackLoop)
- Formatting few-shot examples into prompt text — responsibility of generator agents (0028); this service returns typed records only
- Memory scenario browsing UI — post-MVP, feature 0040

---

## Stories

### US-001: Embed Story and Retrieve Similar Scenarios

**As the** pipeline  
**I want to** embed the parsed story text and query the project's `TestMemory` container for semantically similar past scenarios  
**So that** test generators receive proven past examples as few-shot context to improve scenario quality

#### Acceptance Criteria

- [ ] AC-001: The service receives the `ParsedStory` and `ProjectConfig` (containing `userId` and `projectId`) from the pipeline context.
- [ ] AC-002: The full parsed story text is embedded using Azure OpenAI `text-embedding-3-small` (1536 dimensions).
- [ ] AC-003: The vector search is scoped to the current run's `userId` and `projectId` via the Cosmos DB partition key — no cross-project or cross-user results are returned.
- [ ] AC-004: The Cosmos DiskANN query returns at most 3 entries ordered by cosine similarity (highest first).
- [ ] AC-005: Only entries with `isDeleted: false` are included in the results.
- [ ] AC-006: The result is a `MemoryRetrievalResult` containing an `IReadOnlyList<TestMemoryEntry>` with the retrieved scenarios.
- [ ] AC-007: Each `TestMemoryEntry` exposes `storyText`, `scenarioText`, `testType`, `passRate`, and `runCount`.
- [ ] AC-008: The `MemoryRetrievalResult` is attached to the pipeline context and forwarded to stage 4 generators.

---

### US-002: Graceful Cold Start (No Stored Scenarios)

**As the** pipeline  
**I want** memory retrieval to succeed with zero results when no matching scenarios exist  
**So that** generation runs normally for new projects or projects with no passing test history yet

#### Acceptance Criteria

- [ ] AC-009: When the Cosmos query returns zero documents, `MemoryRetrievalResult.Scenarios` is an empty list.
- [ ] AC-010: No warning or error is emitted when the result set is empty — empty results are the expected cold-start state.
- [ ] AC-011: Generator agents receive the empty result and produce scenarios without few-shot examples.

---

### US-003: Graceful Failure Handling

**As the** pipeline  
**I want** memory retrieval to degrade gracefully when the embedding service or Cosmos query fails  
**So that** a transient infrastructure failure does not block test generation or fail the run

#### Acceptance Criteria

- [ ] AC-012: If the Azure OpenAI embedding call throws any exception, the service catches it, emits a structured warning log, and returns an empty `MemoryRetrievalResult`.
- [ ] AC-013: If the Cosmos DiskANN vector search throws any exception, the service catches it, emits a structured warning log, and returns an empty `MemoryRetrievalResult`.
- [ ] AC-014: In both failure cases, the pipeline continues to stage 4 (generators) with an empty memory result — the run is not marked as failed or degraded in the `TestRun` record.
- [ ] AC-015: The warning log includes `userId`, `projectId`, and run ID to aid diagnostics.

---

### US-004: Expose Typed Result to Generator Agents

**As the** pipeline  
**I want** the memory retrieval output to be a strongly typed record  
**So that** each generator agent can independently decide how to render few-shot examples for its own AI prompt

#### Acceptance Criteria

- [ ] AC-016: `MemoryRetrievalResult` is a C# record defined in `Testurio.Core` with no prompt-formatting logic.
- [ ] AC-017: Generator agents (feature 0028) receive `MemoryRetrievalResult` as a parameter and are solely responsible for rendering its contents into the AI prompt.
- [ ] AC-018: `MemoryRetrievalService` has no knowledge of prompt structure or test-type-specific formatting — it returns raw `TestMemoryEntry` objects only.
