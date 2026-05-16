---
name: Testurio — Feature List
version: 1.6.0
status: draft
updated: 2026-05-15
tags: [features, business, product]
---

# Feature List — Testurio

## Document notes

- Primary user persona: Lead QA engineer automating the testing process
- Features are listed in implementation order
- v1 scope: single-user accounts, web products only, Azure DevOps and Jira integrations
- **POC scope:** features 0001–0005; Jira integration, API testing only, commercial LLM API, hardcoded single project config — **completed**
- **MVP scope:** features 0001–0031 (except 0019); both PM tools, API + UI E2E testing, full pipeline with memory layer, full portal and billing
- **Post-MVP scope:** features 0033–0042; additional test types (smoke, a11y, visual, performance), model tier routing, cross-project memory, training data export

---

## POC — Completed

**[0001]: Automatic Test Run Trigger** `POC`
_Business Outcome: Eliminates manual QA scheduling by starting test runs without human intervention._
When a User Story moves to the "In Testing" status in Jira, the system receives the event via webhook and enqueues an API test run. If the story has no description or acceptance criteria, the QA lead is notified and the run is skipped. Incoming triggers while a run is active are queued and executed sequentially. POC constraints: Jira only, webhook only, User Story issue type only, API testing only, no plan-tier trigger limits.

---

**[0002]: Story-Driven Test Scenario Generation** `POC`
_Business Outcome: Removes the need to write test cases manually for each story._
The system reads the work item's description and acceptance criteria and generates a set of test scenarios tailored to that story. Coverage follows the testing strategy and any custom instructions configured for the project.

---

**[0003]: Automated API Test Execution** `POC`
_Business Outcome: Delivers consistent, repeatable API coverage without QA effort per story._
Generated API test scenarios are executed automatically — the system sends HTTP requests to the configured product URL and validates responses against expected outcomes. The QA lead receives results without writing or running a single test manually.

---

**[0004]: Test Report Delivery to PM Tool** `POC`
_Business Outcome: Keeps all test evidence in the PM tool where the team already works._
After execution, a report is posted directly back to the originating work item as a comment or attachment. The report format and included attachments follow the template the QA lead defined in project settings.

---

**[0005]: Execution Log Capture** `POC`
_Business Outcome: Gives QA leads and developers clear evidence for diagnosing API failures._
During API test execution the system captures a step-by-step log of every request sent and response received. Logs are included in the delivered report according to the project's report settings.

---

## MVP — AI Testing Pipeline

**[0025]: Intelligent Story Parser** `MVP`
_Business Outcome: Ensures every story can be processed regardless of how it was written, while nudging teams toward a consistent format._
The system checks whether the incoming work item matches the Testurio story template (title, description, acceptance criteria). If it matches, the story is parsed directly into structured JSON. If it does not match, the system calls Claude to convert it, posts a warning comment to the originating ADO/Jira ticket, and continues with the converted story. No run is silently skipped due to poor story formatting.

---

**[0026]: Test Generator Router** `MVP`
_Business Outcome: Automatically selects and runs the right test generation strategy per project without QA lead intervention._
After story parsing, the system reads the project's `test_type` configuration (`api | ui_e2e | both`) and resolves which generator agents to invoke. Enabled generators are dispatched in parallel. The router collects all results before passing them to the executor stage.

---

**[0027]: Memory Retrieval Service** `MVP`
_Business Outcome: Improves test scenario quality over time by reusing proven patterns from similar past stories._
Before each generation call, the system embeds the parsed story text using Azure OpenAI `text-embedding-3-small` and runs a vector similarity search against the project's `TestMemory` container in Cosmos DB. The top-3 most semantically similar past scenarios for the given test type are retrieved and injected as few-shot examples into the generator prompt.

---

**[0028]: Test Generator Agents — API & UI E2E** `MVP`
_Business Outcome: Produces high-quality, assertion-complete test scenarios for API and browser flows from a user story alone._
Two generator agents run in parallel — `ApiTestGeneratorAgent` and `UiE2eTestGeneratorAgent`. Each receives the parsed story, the retrieved memory examples, and the project config. Each calls Claude API (`claude-opus-4-7`) with adaptive thinking and outputs a typed scenario JSON array. API scenarios include HTTP method, path, headers, body, and assertions. UI E2E scenarios include browser steps with selectors and assertions.

---

**[0029]: Executor Router — HTTP & Playwright** `MVP`
_Business Outcome: Executes API and UI E2E scenarios automatically, routing each to the correct executor without manual configuration per run._
The executor router dispatches generated scenarios to: `HttpExecutor` for API scenarios (sends HTTP requests, validates status codes, JSON path assertions, and response headers) and `PlaywrightExecutor` for UI E2E scenarios (drives a real browser, captures screenshots at each step). Both executors run in parallel when both test types are enabled. Results include pass/fail status, duration, and assertion details.

---

**[0030]: AI-Powered Report Writer** `MVP`
_Business Outcome: Delivers a readable, actionable test verdict directly in the PM tool where the team already works._
After execution, Claude writes a structured verdict report: a one-sentence PASSED/FAILED verdict, a per-scenario result list with duration and failure diffs, and a closing recommendation (approve, request fixes, or flag for manual review). The report is posted as a comment to the originating ADO/Jira ticket and a `TestResult` record is written to Cosmos for portal statistics.

---

**[0031]: QA Lead Feedback Capture via PM Tool Comments** `MVP`
_Business Outcome: Lets QA leads inject domain knowledge and corrections directly from their PM tool, improving future test generation without any portal interaction._
After a test run, the QA lead can add a comment to the originating ADO or Jira ticket containing `@testurio memorize` anywhere in the text. The system detects this flag during its next comment-poll cycle (or via webhook comment event, depending on project trigger method). The comment body — excluding the flag itself — is embedded using `text-embedding-3-small` and upserted to the `TestMemory` container as a `feedback` entry, scoped to `userId + projectId + testType` inferred from the run. The entry is tagged `source: qalead` and is excluded from the soft-delete quality loop entirely. On the next generation call for the same project, `MemoryRetrieval` returns these feedback entries alongside scenario-derived entries, injecting the QA lead's knowledge as few-shot context. A confirmation reply is posted back to the ticket comment thread so the QA lead knows the note was captured.

---


## MVP — Project Management

**[0006]: Project Creation & Core Configuration** `MVP`
_Business Outcome: Lets QA leads model their real product structure inside Testurio._
User can create a named project and set the product URL and testing strategy (e.g. smoke, regression, BDD). Each project is fully isolated, so different products can have independent configurations.

---

**[0007]: PM Tool Integration** `MVP`
_Business Outcome: Connects Testurio to where stories already live, with no developer involvement after initial setup._
User can connect a project to Azure DevOps or Jira by providing the required access tokens. Once connected, the system receives status-change events and posts reports back automatically.

---

**[0020]: Configurable Work Item Type Filtering** `MVP`
_Business Outcome: Prevents unwanted test runs triggered by irrelevant issue types such as tasks or sub-tasks._
The QA lead can specify which Jira or Azure DevOps issue types are eligible to trigger a test run (e.g. Story, Bug, Task). Changes apply to future triggers only and are configured per project. At least one type must be selected.

---

**[0017]: Testing Environment Access Configuration** `MVP`
_Business Outcome: Lets QA leads connect Testurio to any protected staging environment without involving a security team more than once._
User can choose how Testurio authenticates when accessing the product URL: by allowlisting a set of published static IPs on their firewall or CDN, or by providing HTTP Basic Auth credentials or a custom secret header token stored securely in project settings. Both options are documented with a step-by-step client setup guide so the QA lead can hand the instructions directly to their infrastructure team.

---

**[0022]: Configurable API Request Timeout** `MVP`
_Business Outcome: Prevents slow product APIs from stalling test runs indefinitely while giving QA leads control over acceptable response times._
The QA lead can set a per-request timeout (in seconds) at the project level. If an HTTP request exceeds the timeout, the step is marked as failed and execution continues with the remaining steps. POC uses a fixed hardcoded timeout.

---

**[0023]: Multiple Authentication Methods for API Test Execution** `MVP`
_Business Outcome: Allows Testurio to test APIs that require different authentication schemes without manual request editing._
The QA lead can configure the authentication method used when executing API test requests against the product URL. Supported methods: Bearer token, API key (header or query param), and HTTP Basic Auth. Credentials are stored securely in project settings. POC supports Bearer token only.

---

**[0008]: Custom Test Generation Prompt** `MVP`
_Business Outcome: Allows QA leads to steer test coverage toward the areas that matter most for their product._
User can add an optional prompt to a project that guides the AI when generating test scenarios. This lets the QA lead enforce conventions, restrict scope, or emphasise specific risk areas without changing the global testing strategy.

---

**[0009]: Report Format & Attachment Settings** `MVP`
_Business Outcome: Ensures delivered reports match the team's existing standards and review workflow._
User can define the report template for a project and choose whether screenshots and step-by-step logs are attached to the PM tool report. All reports for that project will consistently follow the chosen format.

---

**[0018]: Automated UI End-to-End Test Execution** `MVP`
_Business Outcome: Extends automated coverage to user-facing flows without the QA lead operating a browser._
The system drives a real browser through the scenarios generated from the story, interacting with the product's UI exactly as a user would. Screenshots are captured at each step and included in the report according to project settings.

---

**[0024]: Automatic Work Item Status Transition After Report Delivery** `MVP`
_Business Outcome: Closes the testing loop automatically by moving work items to the correct status without the QA lead touching Jira after a run completes._
The QA lead can configure, per project, which Jira or Azure DevOps status the work item should be transitioned to after a passed run and after a failed run. If not configured, no transition is made. POC makes no status transitions — report delivery only.

---

**[0021]: Plan-Tier Test Run Quota** `MVP`
_Business Outcome: Ensures infrastructure usage stays within plan boundaries and creates a clear upgrade path._
Each subscription plan defines a maximum number of test run triggers per day. When the quota is reached, additional triggers are rejected and the QA lead is notified with the reset time. The project dashboard shows current usage versus the plan limit. The counter resets at midnight UTC.

---

## MVP — Statistics & Dashboard

**[0010a]: Private Cabinet Main Layout & Navigation** `MVP`
_Business Outcome: Gives every authenticated page a consistent, recognisable shell so QA leads can orient themselves and navigate without friction._
All authenticated pages are rendered inside a shared shell layout. The top header displays the Testurio logo on the left and the signed-in user's avatar and display name on the right. A collapsible left sidebar contains primary navigation links — Dashboard and Projects — followed by Settings. A Sign Out action is pinned at the bottom of the sidebar. The active link is visually highlighted. The layout is the entry point for all portal pages and must be in place before any authenticated page is implemented.

---

**[0010b]: Project List Page** `MVP`
_Business Outcome: Gives the QA lead a central place to see all their projects and jump directly to any of them._
User sees all their projects displayed as a card grid. Each card shows the project name, product URL, and a truncated preview of the testing strategy (first ~120 characters with an ellipsis if longer). A persistent "Create Project" button is always visible at the top of the page and links to the existing project creation form. An empty state guides new users to create their first project. Cards link to the project settings page.

---

**[0010]: Project Dashboard — Snapshot** `MVP`
_Business Outcome: Gives the QA lead an at-a-glance view of testing health across all products._
User sees a card grid of all projects sorted by most recent activity, each showing its latest run status badge, product URL, and testing strategy. A global quota usage bar sits above the grid. An empty state guides new users to create their first project. Cards navigate to per-project history. Real-time updates are added by feature 0043.

---

**[0043]: Project Dashboard — Real-Time Updates** `MVP`
_Business Outcome: Eliminates manual refreshing by keeping run status badges current as the test pipeline progresses._
Run status badges on the dashboard update automatically via Server-Sent Events as the Worker moves through pipeline stages. The client reconnects with exponential back-off on disconnect and falls back to a one-time snapshot re-fetch if all attempts fail. Depends on feature 0010.

---

**[0011]: Per-Project Test History & Trends** `MVP`
_Business Outcome: Enables the QA lead to track quality over time and identify recurring failure patterns._
User can view the full run history for a project, including pass/fail counts and trend charts over time. Individual run reports — with their logs and screenshots — are accessible directly from the history view.

---

## MVP — Public Website

**[0013]: Registration & Sign-In** `MVP`
_Business Outcome: Provides secure, frictionless access for new and returning users._
New visitors can create an account using email/password or a social login in a few steps. Returning users sign in from the same page and land directly in their workspace.

---

**[0012]: Marketing & Pricing Pages** `MVP`
_Business Outcome: Converts interested visitors into registered users by clearly communicating value and cost._
Visitors can read about Testurio's capabilities and compare available subscription plans before committing. The site serves as the primary entry point for new customers.

---

## MVP — Account & Billing

**[0014]: Account Settings** `MVP`
_Business Outcome: Lets users keep their profile and preferences current without contacting support._
User can update personal information, choose display language, and set appearance preferences from a dedicated settings screen.

---

**[0015]: Plan Purchase** `MVP`
_Business Outcome: Enables self-service onboarding with no sales friction._
User can select and purchase a subscription plan directly within the product. The account is activated immediately on successful payment and the user can start creating projects right away.

---

**[0016]: Subscription Management** `MVP`
_Business Outcome: Gives users full control over their subscription without contacting support._
User can view their active plan, update their payment method, and upgrade or cancel their subscription at any time. Changes take effect according to the plan's billing cycle.

---

## Post-MVP — Configuration & Integration

**[0019]: Trigger Notification Method Configuration** `Post-MVP`
_Business Outcome: Lets QA leads choose the integration approach that fits their infrastructure without involving a developer._
When setting up a project, the QA lead can select whether Testurio receives Jira status-change events via webhook or by polling on a configured interval. Both methods are supported for Jira and Azure DevOps. Only one method is active per project at a time.

---

## Post-MVP — Additional Test Types

**[0033]: Smoke Test Generator & Executor** `Post-MVP`
_Business Outcome: Gives QA leads a fast post-deploy sanity check without writing or maintaining smoke tests manually._
Adds `SmokeTestGeneratorAgent` to the generator stage and extends the executor router to dispatch smoke scenarios. Smoke tests cover only the single most critical happy path per story, are limited to 3 steps and 3 scenarios, and must complete in under 30 seconds. Reuses `HttpExecutor` and `PlaywrightExecutor` — no new executor infrastructure required.

---

**[0034]: Accessibility (a11y) Test Generator & Executor** `Post-MVP`
_Business Outcome: Automates WCAG 2.1 AA compliance checking for new UI features at zero additional QA effort._
Adds `A11yTestGeneratorAgent` and extends `PlaywrightExecutor` with axe-core integration. Generated scenarios include full-page axe scans, keyboard navigation flows, ARIA label checks, and color contrast assertions. Each scenario references the relevant WCAG criterion.

---

**[0035]: Visual Regression Test Generator & Executor** `Post-MVP`
_Business Outcome: Catches unintended UI changes before they reach production without manual visual inspection._
Adds `VisualTestGeneratorAgent` and extends `PlaywrightExecutor` with Playwright screenshot comparison. Generated scenarios capture full-page and component-level screenshots across meaningful UI states (default, hover, focus, loading, error, empty). The first run establishes the baseline; subsequent runs diff against it.

---

**[0036]: Performance Test Generator & K6 Executor** `Post-MVP`
_Business Outcome: Surfaces API performance regressions introduced by a story before the change is merged._
Adds `PerformanceTestGeneratorAgent` and a new `K6Executor`. Generated scenarios include three load profiles (baseline 10 VUs, normal 50 VUs, stress 200 VUs) with success thresholds (p95 < 500 ms, error rate < 1%). Scripts are parameterized — no credentials hardcoded.

---

**[0037]: Per-Project Test Type Selection** `Post-MVP`
_Business Outcome: Lets QA leads expand automated coverage incrementally without reconfiguring the whole project._
The QA lead can enable or disable each of the six test types (api, ui_e2e, smoke, a11y, visual, performance) per project. Changes apply to future runs only. At least one type must remain enabled. This setting drives which generator agents and executors fire in the pipeline.

---

## Post-MVP — Intelligence & Optimization

**[0038]: AI Model Tier Routing** `Post-MVP`
_Business Outcome: Reduces generation cost significantly without sacrificing quality on complex stories._
The system routes generation calls across Claude Haiku → Sonnet → Opus based on story complexity score and output quality threshold. Simple stories with familiar patterns use Haiku; stories that fail quality scoring escalate to Sonnet, then Opus. Routing thresholds are configurable per project.

---

**[0039]: Cross-Project Memory Sharing** `Post-MVP`
_Business Outcome: Accelerates quality improvement for new projects by learning from patterns proven across the user's entire account._
When the QA lead opts in, effective scenarios are stored with an anonymized user identity and no project scope, making them retrievable for any project. The QA lead can opt in or out per project at any time.

---

**[0040]: Memory Scenario Viewer** `Post-MVP`
_Business Outcome: Gives QA leads visibility into and control over what the AI has learned, building trust in the memory layer._
The QA lead can browse all stored scenarios for a project, filtered by test type. Each entry shows the original story snippet, scenario summary, pass rate, run count, and last used date. Individual entries can be deleted.

---

**[0041]: Training Data Export** `Post-MVP`
_Business Outcome: Enables the team to distill a smaller, cheaper model trained on their own domain — the prerequisite for cost reduction at scale._
The QA lead or admin can export all stored `(storyText, scenarioJson, outcome)` pairs for a project or account as a JSONL file. This dataset is the input for future model distillation (Phase 3 of the AI cost roadmap).

---

**[0042]: Pipeline Run Detail View** `Post-MVP`
_Business Outcome: Gives QA leads and engineering leads transparency into what the pipeline did and what it cost._
Each test run in the portal shows a per-stage breakdown: which generator agents ran, which executor handled each test type, token usage per Claude call, and estimated cost. Helps identify expensive runs and tune project configuration.

---

**[0032]: Memory Writer Service** `Post-MVP`
_Business Outcome: Makes the memory layer self-filling by automatically capturing proven test patterns from every passing run, compounding generation quality without QA lead effort._
When all scenarios in a run pass, the system embeds the parsed story text using `text-embedding-3-small` and upserts the scenario JSON to the Cosmos `TestMemory` container, tagged `source: pipeline`. The stored entry includes the story text, scenario JSON, test type, `userId`, and `projectId`. Complements feature 0031 (manual QA lead feedback) by automating memory growth at scale.
