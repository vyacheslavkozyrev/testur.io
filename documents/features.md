---
name: Testurio — Feature List
version: 1.3.0
status: draft
updated: 2026-04-27
tags: [features, business, product]
---

# Feature List — Testurio

## Document notes

- Primary user persona: Lead QA engineer automating the testing process
- MVP priority order: AI Testing Pipeline → Public Website → Personal Web Panel → Billing
- v1 scope: single-user accounts, web products only, Azure DevOps and Jira integrations
- **POC scope:** features 0001–0005; Jira integration, API testing only, commercial LLM API, hardcoded single project config
- **MVP scope:** all 24 features; both PM tools, API + UI E2E testing, self-hosted LLM, full portal and billing

---

### AI Testing Pipeline

**[0001]: Automatic Test Run Trigger** `POC`
_Business Outcome: Eliminates manual QA scheduling by starting test runs without human intervention._
When a User Story moves to the "In Testing" status in Jira, the system receives the event via webhook and enqueues an API test run. If the story has no description or acceptance criteria, the QA lead is notified and the run is skipped. Incoming triggers while a run is active are queued and executed sequentially. POC constraints: Jira only, webhook only, User Story issue type only, API testing only, no plan-tier trigger limits.

---

**[0019]: Trigger Notification Method Configuration** `MVP`
_Business Outcome: Lets QA leads choose the integration approach that fits their infrastructure without involving a developer._
When setting up a project, the QA lead can select whether Testurio receives Jira status-change events via webhook or by polling on a configured interval. Both methods are supported for Jira and Azure DevOps. Only one method is active per project at a time.

---

**[0020]: Configurable Work Item Type Filtering** `MVP`
_Business Outcome: Prevents unwanted test runs triggered by irrelevant issue types such as tasks or sub-tasks._
The QA lead can specify which Jira or Azure DevOps issue types are eligible to trigger a test run (e.g. Story, Bug, Task). Changes apply to future triggers only and are configured per project. At least one type must be selected.

---

**[0021]: Plan-Tier Test Run Quota** `MVP`
_Business Outcome: Ensures infrastructure usage stays within plan boundaries and creates a clear upgrade path._
Each subscription plan defines a maximum number of test run triggers per day. When the quota is reached, additional triggers are rejected and the QA lead is notified with the reset time. The project dashboard shows current usage versus the plan limit. The counter resets at midnight UTC.

---

**[0002]: Story-Driven Test Scenario Generation** `POC`
_Business Outcome: Removes the need to write test cases manually for each story._
The system reads the work item's description and acceptance criteria and generates a set of test scenarios tailored to that story. Coverage follows the testing strategy and any custom instructions configured for the project.

---

**[0003]: Automated API Test Execution** `POC`
_Business Outcome: Delivers consistent, repeatable API coverage without QA effort per story._
Generated API test scenarios are executed automatically — the system sends HTTP requests to the configured product URL and validates responses against expected outcomes. The QA lead receives results without writing or running a single test manually.

---

**[0022]: Configurable API Request Timeout** `MVP`
_Business Outcome: Prevents slow product APIs from stalling test runs indefinitely while giving QA leads control over acceptable response times._
The QA lead can set a per-request timeout (in seconds) at the project level. If an HTTP request exceeds the timeout, the step is marked as failed and execution continues with the remaining steps. POC uses a fixed hardcoded timeout.

---

**[0023]: Multiple Authentication Methods for API Test Execution** `MVP`
_Business Outcome: Allows Testurio to test APIs that require different authentication schemes without manual request editing._
The QA lead can configure the authentication method used when executing API test requests against the product URL. Supported methods: Bearer token, API key (header or query param), and HTTP Basic Auth. Credentials are stored securely in project settings. POC supports Bearer token only.

---

**[0004]: Test Report Delivery to PM Tool** `POC`
_Business Outcome: Keeps all test evidence in the PM tool where the team already works._
After execution, a report is posted directly back to the originating work item as a comment or attachment. The report format and included attachments follow the template the QA lead defined in project settings.

---

**[0024]: Automatic Work Item Status Transition After Report Delivery** `MVP`
_Business Outcome: Closes the testing loop automatically by moving work items to the correct status without the QA lead touching Jira after a run completes._
The QA lead can configure, per project, which Jira or Azure DevOps status the work item should be transitioned to after a passed run and after a failed run. If not configured, no transition is made. POC makes no status transitions — report delivery only.

---

**[0005]: Execution Log Capture** `POC`
_Business Outcome: Gives QA leads and developers clear evidence for diagnosing API failures._
During API test execution the system captures a step-by-step log of every request sent and response received. Logs are included in the delivered report according to the project's report settings.

---

### Project Management

**[0006]: Project Creation & Core Configuration** `MVP`
_Business Outcome: Lets QA leads model their real product structure inside Testurio._
User can create a named project and set the product URL and testing strategy (e.g. smoke, regression, BDD). Each project is fully isolated, so different products can have independent configurations.

---

**[0007]: PM Tool Integration** `MVP`
_Business Outcome: Connects Testurio to where stories already live, with no developer involvement after initial setup._
User can connect a project to Azure DevOps or Jira by providing the required access tokens. Once connected, the system receives status-change events and posts reports back automatically.

---

**[0008]: Custom Test Generation Prompt** `MVP`
_Business Outcome: Allows QA leads to steer test coverage toward the areas that matter most for their product._
User can add an optional prompt to a project that guides the AI when generating test scenarios. This lets the QA lead enforce conventions, restrict scope, or emphasise specific risk areas without changing the global testing strategy.

---

**[0009]: Report Format & Attachment Settings** `MVP`
_Business Outcome: Ensures delivered reports match the team's existing standards and review workflow._
User can define the report template for a project and choose whether screenshots and step-by-step logs are attached to the PM tool report. All reports for that project will consistently follow the chosen format.

---

**[0017]: Testing Environment Access Configuration** `MVP`
_Business Outcome: Lets QA leads connect Testurio to any protected staging environment without involving a security team more than once._
User can choose how Testurio authenticates when accessing the product URL: by allowlisting a set of published static IPs on their firewall or CDN, or by providing HTTP Basic Auth credentials or a custom secret header token stored securely in project settings. Both options are documented with a step-by-step client setup guide so the QA lead can hand the instructions directly to their infrastructure team.

---

**[0018]: Automated UI End-to-End Test Execution** `MVP`
_Business Outcome: Extends automated coverage to user-facing flows without the QA lead operating a browser._
The system drives a real browser through the scenarios generated from the story, interacting with the product's UI exactly as a user would. Screenshots are captured at each step and included in the report according to project settings.

---

### Statistics & Dashboard

**[0010]: Project Dashboard** `MVP`
_Business Outcome: Gives the QA lead an at-a-glance view of testing health across all products._
User sees a summary of all projects and the status of their most recent test runs on a single screen. This makes it easy to spot failures or stalled runs without opening each project individually.

---

**[0011]: Per-Project Test History & Trends** `MVP`
_Business Outcome: Enables the QA lead to track quality over time and identify recurring failure patterns._
User can view the full run history for a project, including pass/fail counts and trend charts over time. Individual run reports — with their logs and screenshots — are accessible directly from the history view.

---

### Public Website

**[0012]: Marketing & Pricing Pages** `MVP`
_Business Outcome: Converts interested visitors into registered users by clearly communicating value and cost._
Visitors can read about Testurio's capabilities and compare available subscription plans before committing. The site serves as the primary entry point for new customers.

---

**[0013]: Registration & Sign-In** `MVP`
_Business Outcome: Provides secure, frictionless access for new and returning users._
New visitors can create an account using email/password or a social login in a few steps. Returning users sign in from the same page and land directly in their workspace.

---

### Account & Billing

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
