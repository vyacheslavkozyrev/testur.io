# User Stories — Structured Plan Limits & Feature Enforcement (0046)

## Out of Scope

The following are explicitly **not** part of this feature:

- Per-project daily run quota (feature 0021 enforcement logic migrates here; the old daily counter is retired)
- Adding new plan tiers or changing plan prices — `PlanDocument` schema change only
- Stripe integration changes — plan limits are read from Cosmos, not from Stripe metadata
- Team / multi-seat entitlement logic — v1 is single-user only
- Displaying feature flags on the pricing page — `displayFeatures` (marketing copy) is unchanged; this feature only adds `limits` and `features` typed sections
- Enforcement of post-MVP feature flags (`smoke`, `a11y`, `visual`, `performance`) — only MVP flags are enforced
- Graceful degradation of already-running pipeline stages — enforcement fires before enqueue; an in-flight run is never interrupted
- Usage analytics or billing dashboards beyond the existing quota bar

---

## Stories

### US-001: Plan document carries typed enforcement sections

**As the** Testurio system
**I want to** read structured `limits` and `features` objects from the `PlanDocument` stored in Cosmos DB
**So that** enforcement logic can consume machine-readable ceilings and capability flags instead of parsing unstructured marketing strings

#### Acceptance Criteria

- [ ] AC-001: `PlanDocument` is extended with two new required top-level objects: `limits` (type `PlanLimits`) and `features` (type `PlanFeatures`)
- [ ] AC-002: `PlanLimits` contains: `maxProjects` (int, `-1` = unlimited) and `maxTestRunsPerMonth` (int, `-1` = unlimited)
- [ ] AC-003: `PlanFeatures` contains boolean flags: `apiTesting`, `uiE2eTesting`, `aiMemory`, `pmReportPostBack`
- [ ] AC-004: The existing `Features` string list on `PlanDocument` is renamed to `DisplayFeatures` and remains populated with the same marketing copy strings; no pricing-page UI change is needed
- [ ] AC-005: All four existing plan documents seeded in Cosmos (`test-junior`, `test-pro`, `team`, `centurio`) are updated with correct `limits` and `features` values matching the plan tier definitions; at minimum `test-junior` sets `maxProjects: 3`, `maxTestRunsPerMonth: 50`, and all feature flags to `true` except `aiMemory: false`
- [ ] AC-006: The `GET /v1/plans` endpoint returns a `PlanDefinitionDto` that includes `limits` and `features` fields alongside `displayFeatures`; the response schema change is backward-compatible (additive only)
- [ ] AC-007: `IPlanRepository` exposes a `GetByPlanAsync(SubscriptionPlan plan, CancellationToken ct)` method in addition to `ListAllAsync`, enabling single-plan lookups by the enforcement layer without fetching all plans

---

### US-002: Project creation is rejected when `maxProjects` limit is reached

**As a** QA lead on a plan with a project ceiling
**I want to** receive an immediate, informative rejection when I try to create a project beyond my plan's limit
**So that** I understand why the creation failed and know which plan to upgrade to

#### Acceptance Criteria

- [ ] AC-008: Before creating a project, `ProjectService.CreateAsync` calls `IPlanEnforcementService.CheckProjectLimitAsync(userId, cancellationToken)`
- [ ] AC-009: `CheckProjectLimitAsync` reads the user's active subscription plan, resolves the corresponding `PlanDocument`, counts the user's non-deleted projects in Cosmos, and throws `PlanLimitExceededException` when the count equals or exceeds `limits.maxProjects` (the check is skipped when `maxProjects == -1`)
- [ ] AC-010: `PlanLimitExceededException` carries `LimitName: "maxProjects"` and `RequiredPlan: <next tier name>` in its payload
- [ ] AC-011: The global `GlobalExceptionHandler` maps `PlanLimitExceededException` to `403 Forbidden` with a `ProblemDetails` body whose `extensions` include `limitName` and `requiredPlan` fields
- [ ] AC-012: The `ProblemDetails.Title` for a project-limit rejection is `"Plan limit reached"` and `Detail` is `"Your plan allows a maximum of {n} projects. Upgrade to {plan} to create more."`
- [ ] AC-013: When `maxProjects == -1` (unlimited plan), project creation proceeds without any limit check
- [ ] AC-014: The rejection fires before any Cosmos write — no partial document is created

---

### US-003: Test run trigger is rejected when `maxTestRunsPerMonth` quota is exhausted

**As a** QA lead whose monthly test run quota is exhausted
**I want to** receive a clear rejection when a webhook or manual trigger fires
**So that** I understand the quota boundary and can plan an upgrade before the next billing cycle

#### Acceptance Criteria

- [ ] AC-015: Before enqueuing a test run job (in the webhook handler that writes the `TestRun` document and publishes to Service Bus), `IPlanEnforcementService.CheckMonthlyRunQuotaAsync(userId, cancellationToken)` is called
- [ ] AC-016: `CheckMonthlyRunQuotaAsync` counts `TestRun` documents for `userId` where `createdAt >= first day of current UTC month` and throws `PlanLimitExceededException` when the count equals or exceeds `limits.maxTestRunsPerMonth` (skipped when `maxTestRunsPerMonth == -1`)
- [ ] AC-017: The exception carries `LimitName: "maxTestRunsPerMonth"` and `RequiredPlan: <next tier>`
- [ ] AC-018: The webhook endpoint returns `403 Forbidden` with a `ProblemDetails` body using the same structure as AC-011 when quota is exceeded
- [ ] AC-019: `ProblemDetails.Detail` is `"Your plan allows {n} test runs per month. Your quota resets on {first day of next month, ISO 8601 date}. Upgrade to {plan} to run more tests."`
- [ ] AC-020: When `maxTestRunsPerMonth == -1`, the quota check is skipped entirely and the run is enqueued normally
- [ ] AC-021: Feature 0021's daily counter (`usedToday` / `dailyLimit`) is superseded by `maxTestRunsPerMonth`; the `quotaUsage` object in `GET /v1/stats/dashboard` is updated to expose `usedThisMonth` and `monthlyLimit` (and `resetsAt` becomes the first day of the next UTC month); the old `usedToday` / `dailyLimit` fields are removed
- [ ] AC-022: The quota count is always a real-time Cosmos read — no separate counter document is maintained; the count query is partition-key–scoped to `userId`

---

### US-004: Worker pipeline skips `MemoryRetrieval` and `MemoryWriter` when `aiMemory` flag is false

**As the** Testurio worker pipeline
**I want to** read the `aiMemory` feature flag from the user's plan before each run
**So that** users on plans without the AI memory feature are not billed for embedding calls and do not populate the memory store

#### Acceptance Criteria

- [ ] AC-023: `TestRunJobProcessor.ExecutePipelineAsync` reads the user's plan `features.aiMemory` flag before invoking Stage 3 (`MemoryRetrieval`)
- [ ] AC-024: When `features.aiMemory == false`, Stage 3 (`MemoryRetrieval`) is skipped; an empty `MemoryRetrievalResult` is passed to Stage 4 (generators), which continue normally
- [ ] AC-025: When `features.aiMemory == false`, Stage 8 (`MemoryWriter`) is not invoked; the pipeline completes without writing to `TestMemory`
- [ ] AC-026: When `features.aiMemory == true` (or the plan cannot be resolved — fail-open), both Stage 3 and Stage 8 execute as normal
- [ ] AC-027: A `[LoggerMessage]` at `Debug` level records when memory stages are skipped, including the `TestRunId` and plan name
- [ ] AC-028: The `MemoryWriter` stage (Stage 8) is added to `TestRunJobProcessor` in this feature; it calls `IMemoryWriterService` after a full-pass run and is gated by the `aiMemory` flag

---

### US-005: Worker pipeline skips `ReportWriter` PM post-back when `pmReportPostBack` flag is false

**As the** Testurio worker pipeline
**I want to** read the `pmReportPostBack` feature flag before posting the test report comment to Jira or ADO
**So that** users on plans without PM tool post-back do not generate unexpected PM tool API calls or incur comment rate-limit issues

#### Acceptance Criteria

- [ ] AC-029: `TestRunJobProcessor.RunReportWriterStageAsync` reads the user's plan `features.pmReportPostBack` flag
- [ ] AC-030: When `features.pmReportPostBack == false`, `IReportWriter.WriteAsync` is still called to generate the `TestResult` record in Cosmos, but the PM tool comment post step inside `ReportWriter` is skipped
- [ ] AC-031: `IReportWriter` (or its concrete implementation) accepts a `bool postBackEnabled` parameter or reads the flag from a plan context object passed through the pipeline; it must NOT call the Jira/ADO comment API when the flag is false
- [ ] AC-032: The `TestRun` and `TestResult` records are always persisted regardless of the `pmReportPostBack` flag
- [ ] AC-033: A `[LoggerMessage]` at `Information` level records when PM post-back is skipped for a run

---

### US-006: Worker pipeline rejects `apiTesting` and `uiE2eTesting` scenarios when feature flags are false

**As the** Testurio worker pipeline
**I want to** check `features.apiTesting` and `features.uiE2eTesting` before dispatching generators and executors
**So that** users on plans that exclude a test type do not consume LLM tokens or executor infrastructure for that type

#### Acceptance Criteria

- [ ] AC-034: After `AgentRouter` resolves `resolvedTestTypes`, `TestRunJobProcessor` filters the list against the plan `features` flags: if `features.apiTesting == false`, `TestType.Api` is removed from the list; if `features.uiE2eTesting == false`, `TestType.UiE2e` is removed
- [ ] AC-035: If the filtered list is empty (all resolved types were disabled by the plan), the run is marked `Skipped` with reason `"Plan does not include any enabled test type for this run"` and the pipeline exits without invoking generators or executors
- [ ] AC-036: When `features.apiTesting == true` and `features.uiE2eTesting == true`, no filtering occurs and the pipeline behaves exactly as before this feature
- [ ] AC-037: The `TestRun.SkipReason` is set and persisted to Cosmos before the pipeline returns when skipped due to plan feature flags

---

### US-007: Dashboard quota bar displays monthly usage

**As a** QA lead
**I want to** see my monthly test run usage against my plan's monthly limit on the dashboard
**So that** I know how many runs remain before quota exhaustion without guessing

#### Acceptance Criteria

- [ ] AC-038: `GET /v1/stats/dashboard` returns a `quotaUsage` object with fields: `usedThisMonth` (int), `monthlyLimit` (int, `-1` for unlimited), `resetsAt` (ISO 8601 UTC — first day of the next calendar month)
- [ ] AC-039: The dashboard UI displays "X / Y runs used this month" (or "Unlimited" when `monthlyLimit == -1`) and "Resets on {formatted date}" instead of the old "today" / "midnight UTC" copy
- [ ] AC-040: When `monthlyLimit == -1`, the quota bar renders in a neutral style with "Unlimited" replacing the numeric limit; no amber or red highlighting is applied
- [ ] AC-041: When `usedThisMonth >= monthlyLimit` and `monthlyLimit != -1`, the bar renders in red
- [ ] AC-042: When `usedThisMonth >= monthlyLimit * 0.8` and `monthlyLimit != -1`, the bar renders in amber (warning state)
- [ ] AC-043: The `quotaUsage` object is computed from a live Cosmos count of `TestRun` documents for the current month — no cached counter is read

---

### US-008: Enforcement service reads plan with subscription fallback

**As the** Testurio API and Worker
**I want to** resolve a user's effective plan limits in a single, consistent service call
**So that** enforcement checks are centralized and do not scatter subscription + plan reads across the codebase

#### Acceptance Criteria

- [ ] AC-044: `IPlanEnforcementService` is defined in `Testurio.Core` with methods: `CheckProjectLimitAsync`, `CheckMonthlyRunQuotaAsync`, and `GetEffectivePlanAsync` (returns `PlanDocument` or `null`)
- [ ] AC-045: `PlanEnforcementService` in `Testurio.Api` (or shared infrastructure) resolves the plan by: reading `UserSubscription` → mapping `SubscriptionPlan` enum → calling `IPlanRepository.GetByPlanAsync`
- [ ] AC-046: If the user has no active subscription (`UserSubscription` is null or status is `None`/`Expired`), `GetEffectivePlanAsync` returns `null`; enforcement checks treat null as "no active plan" and throw `PlanLimitExceededException` for any finite limit
- [ ] AC-047: `PlanEnforcementService` is registered in `Testurio.Infrastructure` DI and is available in both `Testurio.Api` and `Testurio.Worker` via the shared DI registration
- [ ] AC-048: The service is covered by unit tests that mock `IUserSubscriptionRepository` and `IPlanRepository`
