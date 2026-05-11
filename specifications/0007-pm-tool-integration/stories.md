# User Stories — PM Tool Integration (0007)

## Out of Scope

The following are explicitly **not** part of this feature:

- Webhook event processing and test run triggering — covered by feature 0001
- Polling as an alternative trigger method — covered by feature 0019
- Work item type filtering (which issue types trigger a run) — covered by feature 0020
- Work item status transitions after a test run completes — covered by feature 0024
- Report format and attachment settings for PM tool comments — covered by feature 0009
- ADO and Jira API rate limiting or retry logic — infrastructure concern handled outside this feature
- Support for more than one PM tool connection per project — one tool per project only
- Team-level or organization-level PM tool connections — per-project configuration only
- Polling trigger configuration UI — covered by feature 0019
- Mobile or CLI setup flows — portal only

---

## Stories

### US-001: Connect a Project to Azure DevOps

**As a** QA lead
**I want to** connect my Testurio project to Azure DevOps by providing an organization URL, project name, team, "In Testing" status name, and an access token
**So that** the system can receive status-change events and post test reports back to my ADO work items

#### Acceptance Criteria

- [ ] AC-001: The project settings page contains an "Integrations" section where the user can add a PM tool connection
- [ ] AC-002: Selecting Azure DevOps as the PM tool presents a form with the following required fields: Organization URL, Project Name, Team, "In Testing" Status Name, and Auth Method selector
- [ ] AC-003: The Auth Method selector offers two options for ADO: Personal Access Token (PAT) and OAuth
- [ ] AC-004: When PAT is selected, a required PAT field is displayed; when OAuth is selected, the OAuth authorization flow is initiated instead of a text field
- [ ] AC-005: Submitting a valid ADO form stores the connection configuration in the project's Cosmos DB document; the PAT or OAuth token is stored in Azure Key Vault under the project's namespace and only a Key Vault secret URI is persisted in Cosmos
- [ ] AC-006: The API returns `200 OK` with the updated project document (excluding any secret values) on successful save
- [ ] AC-007: The saved ADO configuration fields visible in the response are: `pmTool: "ado"`, `adoOrgUrl`, `adoProjectName`, `adoTeam`, `adoInTestingStatus`, `adoAuthMethod`, and `adoTokenSecretUri` (no raw token)
- [ ] AC-008: If the project already has a PM tool connection, saving a new one replaces the previous connection entirely and removes the old secret from Key Vault
- [ ] AC-009: All required ADO fields (Organization URL, Project Name, Team, In Testing Status Name, and token or OAuth grant) must be non-empty; submitting with any missing shows an inline validation error and the API returns `400 Bad Request` with a `ValidationProblemDetails` body

---

### US-002: Connect a Project to Jira

**As a** QA lead
**I want to** connect my Testurio project to Jira by providing a base URL, project key, "In Testing" status name, and an access token
**So that** the system can receive status-change events and post test reports back to my Jira issues

#### Acceptance Criteria

- [ ] AC-010: Selecting Jira as the PM tool presents a form with the following required fields: Base URL, Project Key, "In Testing" Status Name, and Auth Method selector
- [ ] AC-011: The Auth Method selector offers two options for Jira: API Token + Email and Personal Access Token (PAT)
- [ ] AC-012: When API Token + Email is selected, both an Email field and an API Token field are displayed and required; when PAT is selected, only a PAT field is displayed and required
- [ ] AC-013: Submitting a valid Jira form stores the connection configuration in the project's Cosmos DB document; the token and email (if applicable) are stored in Azure Key Vault under the project's namespace and only Key Vault secret URIs are persisted in Cosmos
- [ ] AC-014: The API returns `200 OK` with the updated project document (excluding any secret values) on successful save
- [ ] AC-015: The saved Jira configuration fields visible in the response are: `pmTool: "jira"`, `jiraBaseUrl`, `jiraProjectKey`, `jiraInTestingStatus`, `jiraAuthMethod`, and credential secret URIs (no raw token or email)
- [ ] AC-016: All required Jira fields (Base URL, Project Key, In Testing Status Name, and the selected auth credentials) must be non-empty; submitting with any missing shows an inline validation error and the API returns `400 Bad Request` with a `ValidationProblemDetails` body
- [ ] AC-017: Base URL must be a valid URL format; an invalid URL format produces an inline validation error on that field

---

### US-003: Test the PM Tool Connection via a Dedicated Button

**As a** QA lead
**I want to** verify my PM tool connection is working by clicking a "Test Connection" button after saving
**So that** I know the credentials are valid and the system can actually reach my PM tool before any real test run fires

#### Acceptance Criteria

- [ ] AC-018: A "Test Connection" button is displayed in the Integrations section after a PM tool connection has been saved; it is not shown before a connection exists
- [ ] AC-019: Clicking "Test Connection" sends a lightweight API call from the backend to the configured PM tool (ADO or Jira) using the stored credentials, such as fetching project metadata or current user info
- [ ] AC-020: If the PM tool responds with a success status, the portal displays a success indicator ("Connection successful") adjacent to the button
- [ ] AC-021: If the PM tool responds with a 401 or 403 status, the portal displays an error indicator ("Authentication failed — check your token") adjacent to the button
- [ ] AC-022: If the PM tool is unreachable (timeout or DNS failure), the portal displays an error indicator ("Connection failed — check the URL") adjacent to the button
- [ ] AC-023: The "Test Connection" backend endpoint returns a structured result object: `{ status: "ok" | "auth_error" | "unreachable", message: string }` with HTTP `200` in all cases (the HTTP status reflects the Testurio API call, not the downstream PM tool result)
- [ ] AC-024: The "Test Connection" action uses the credentials already stored in Key Vault; it does not re-accept or re-transmit raw token values

---

### US-004: Set Up a Webhook Manually

**As a** QA lead
**I want to** see the Testurio webhook URL and secret displayed in the portal so I can paste them into my PM tool's webhook settings myself
**So that** events reach Testurio after I configure the webhook in my PM tool

#### Acceptance Criteria

- [ ] AC-025: When a PM tool connection has been saved, the Integrations section displays a "Webhook setup" panel
- [ ] AC-026: The webhook setup panel shows two values the user must copy: the Testurio webhook endpoint URL (e.g. `https://api.testur.io/webhooks/ado`) and a per-project webhook secret string
- [ ] AC-027: Each value has a "Copy to clipboard" button beside it
- [ ] AC-028: The webhook secret is generated at PM tool connection save time, stored in Key Vault, and displayed to the user exactly once in plaintext on first setup; subsequent views of the Integrations section mask the secret (e.g. `••••••••`) with a "Regenerate" option
- [ ] AC-029: The webhook setup panel includes step-by-step instructions tailored to the connected PM tool (ADO or Jira) describing where to paste the URL and secret in that tool's UI
- [ ] AC-030: If the user clicks "Regenerate", a new secret is generated, stored in Key Vault replacing the old one, and displayed in plaintext once; the user is warned that the old secret is immediately invalidated and any existing webhook in the PM tool must be updated
- [ ] AC-031: The webhook endpoint URL stored in Cosmos is the Testurio public endpoint URL (e.g. `https://api.testur.io/webhooks/ado`) and never a localhost or internal address

---

### US-005: View the Current PM Tool Integration Status

**As a** QA lead
**I want to** see the current integration status for my project at a glance in the project settings
**So that** I can quickly confirm whether the connection is configured and the webhook is set up before a test run is triggered

#### Acceptance Criteria

- [ ] AC-032: The Integrations section of the project settings page shows one of two states: "Not configured" (no PM tool connection saved) or "Configured" (connection saved)
- [ ] AC-033: When the state is "Not configured", a prompt and button to add a PM tool connection are displayed
- [ ] AC-034: When the state is "Configured", the panel shows the connected PM tool name (Azure DevOps or Jira), the project/org identifier, and the auth method label (not the token value)
- [ ] AC-035: No secret values (tokens, passwords, webhook secrets) are ever returned in any GET project response; only labels and URIs are included
- [ ] AC-036: The Integrations section is accessible from the project settings page without requiring any additional navigation steps beyond opening that page

---

### US-006: Handle an Expired or Invalid PM Tool Token

**As a** QA lead
**I want to** receive an in-portal alert when Testurio detects that my PM tool token has expired or been revoked
**So that** I can update the token in place and restore the integration without disconnecting and reconnecting from scratch

#### Acceptance Criteria

- [ ] AC-037: When the backend receives a 401 response from the PM tool API during any operation (connection test, report posting), it sets an `integrationStatus: "auth_error"` flag on the project document in Cosmos
- [ ] AC-038: The portal displays a persistent alert banner in the Integrations section when `integrationStatus` is `"auth_error"`: "Your PM tool token is invalid or expired — update it below to restore the integration"
- [ ] AC-039: The alert banner contains an inline form (or an "Update Token" button that expands one) allowing the user to enter a new token for the same auth method without altering any other integration settings
- [ ] AC-040: Saving an updated token stores the new value in Key Vault (replacing the old secret at the same URI), clears `integrationStatus` back to `"active"`, and dismisses the alert banner
- [ ] AC-041: Saving an updated token does not require the user to re-enter the Organization URL, Project Name, "In Testing" status name, or any other previously saved fields — only the token is updated
- [ ] AC-042: After the token is updated, the user can immediately click "Test Connection" (US-003) to confirm the new token works before the next real trigger fires

---

### US-007: Remove a PM Tool Integration

**As a** QA lead
**I want to** disconnect and remove the PM tool integration from my project
**So that** Testurio stops receiving events and posting reports for that project

#### Acceptance Criteria

- [ ] AC-043: A "Remove Integration" action is accessible from the Integrations section of the project settings page, behind a confirmation dialog
- [ ] AC-044: The confirmation dialog warns the user that removing the integration will immediately stop any active test run for this project and clear any queued test jobs
- [ ] AC-045: Confirming removal deletes the PM tool configuration fields from the project's Cosmos DB document and schedules deletion of all related Key Vault secrets (token, webhook secret) within the same operation
- [ ] AC-046: Any test run that is currently active for the project is stopped and any pending jobs for that project are removed from the Service Bus queue before the removal operation completes
- [ ] AC-047: The API returns `200 OK` with the updated project document (showing no PM tool configuration) after successful removal
- [ ] AC-048: After removal, the Integrations section returns to the "Not configured" state (AC-033) and the webhook registered with the PM tool (if any) is deregistered via the PM tool API if the token is still valid; if deregistration fails (token already invalid), it is skipped silently and logged
- [ ] AC-049: A user can only remove the integration on projects that belong to their own `userId`; attempting to do so on another user's project returns `403 Forbidden`
