# User Stories — Testing Environment Access Configuration (0017)

## Out of Scope

The following are explicitly **not** part of this feature:

- Live connectivity check to the product URL at save time — format validation only; reachability is not verified
- Support for OAuth 2.0, SAML, or any other authentication scheme beyond IP allowlist, HTTP Basic Auth, and custom header token
- Per-environment configuration (e.g. separate credentials for staging vs. preview) — one access method per project
- Automatic IP allowlist provisioning on the client's firewall or CDN — only documentation and published IP ranges are provided
- Rotation or expiry policies for stored credentials — secrets are updated manually by the QA lead
- Cross-project credential sharing — each project stores its own access configuration independently
- Audit log of who changed credentials and when — covered by a future security feature
- Credential verification at save time (e.g. sending a test request) — saving is always accepted; execution failures surface later

---

## Stories

### US-001: Choose IP Allowlisting as the Access Method

**As a** QA lead
**I want to** select IP allowlisting as the access method for my project
**So that** I can hand off the published static IP range to my infrastructure team and let the worker connect to our staging environment without sharing credentials

#### Acceptance Criteria

- [ ] AC-001: The project settings page presents a choice of three access modes: "IP Allowlisting", "HTTP Basic Auth", and "Custom Header Token"
- [ ] AC-002: Selecting "IP Allowlisting" requires no credential input — no additional fields are shown
- [ ] AC-003: Saving with "IP Allowlisting" selected persists `access_mode: "ip_allowlist"` on the project document in Cosmos DB
- [ ] AC-004: The UI displays the Testurio published egress IP range alongside the IP Allowlisting option with a step-by-step client setup guide (copy IPs → add to firewall/CDN allowlist → done)
- [ ] AC-005: The API returns `200 OK` with the updated project document on successful save
- [ ] AC-006: Switching to IP Allowlisting from another mode clears any previously stored credentials from Key Vault (or marks them as inactive) and removes their secret URIs from the Cosmos project document

---

### US-002: Configure HTTP Basic Auth Credentials

**As a** QA lead
**I want to** configure an HTTP Basic Auth username and password for my project
**So that** the test executor can authenticate against our Basic Auth-protected staging environment automatically

#### Acceptance Criteria

- [ ] AC-007: Selecting "HTTP Basic Auth" reveals two required fields: "Username" and "Password"
- [ ] AC-008: Both fields must be non-empty; submitting with either empty shows an inline validation error and the form is not submitted
- [ ] AC-009: On save, the username and password are stored as individual secrets in Azure Key Vault under the project's namespace (e.g. `projects/{projectId}/basic-auth-user` and `projects/{projectId}/basic-auth-pass`)
- [ ] AC-010: Only the Key Vault secret URIs are persisted in the Cosmos DB project document (`basicAuthSecretUri` fields); the plaintext credentials are never written to Cosmos
- [ ] AC-011: The Password field always renders as masked input (`type="password"`) and is never exposed in API responses
- [ ] AC-012: After saving, the form re-renders with the Username pre-filled and the Password field empty (placeholder: "••••••••") to indicate a password is stored without revealing it
- [ ] AC-013: Switching away from "HTTP Basic Auth" to another mode clears the associated Key Vault secrets and removes the secret URIs from the Cosmos document
- [ ] AC-014: The API returns `200 OK` with the updated project document (containing only the secret URIs, not plaintext values) on successful save

---

### US-003: Configure a Custom Header Token

**As a** QA lead
**I want to** configure a custom secret header name and value for my project
**So that** the test executor can pass the token on every request to our header-protected staging environment

#### Acceptance Criteria

- [ ] AC-015: Selecting "Custom Header Token" reveals two required fields: "Header Name" (e.g. `X-Testurio-Token`) and "Header Value" (the secret)
- [ ] AC-016: Both fields must be non-empty; submitting with either empty shows an inline validation error and the form is not submitted
- [ ] AC-017: Header Name must be a valid HTTP header name (alphanumeric characters and hyphens only, no spaces); an invalid value shows an inline validation error
- [ ] AC-018: On save, the header value is stored as a secret in Azure Key Vault under the project's namespace (e.g. `projects/{projectId}/header-token-value`); the header name is stored as a non-sensitive field in the Cosmos project document directly
- [ ] AC-019: Only the Key Vault secret URI for the header value is persisted in the Cosmos DB project document (`headerTokenSecretUri`); the plaintext token value is never written to Cosmos
- [ ] AC-020: The Header Value field always renders as masked input (`type="password"`) and is never exposed in API responses
- [ ] AC-021: After saving, the form re-renders with the Header Name pre-filled and the Header Value field empty (placeholder: "••••••••") to indicate a value is stored without revealing it
- [ ] AC-022: Switching away from "Custom Header Token" to another mode clears the associated Key Vault secret and removes the secret URI from the Cosmos document
- [ ] AC-023: The API returns `200 OK` with the updated project document on successful save

---

### US-004: View the Current Access Configuration

**As a** QA lead
**I want to** see which access method is currently active for my project
**So that** I can verify the configuration without navigating away or contacting support

#### Acceptance Criteria

- [ ] AC-024: Opening the project settings page shows the currently saved access mode pre-selected
- [ ] AC-025: When HTTP Basic Auth is active, the Username field is pre-filled; the Password field shows the placeholder "••••••••" with no value
- [ ] AC-026: When Custom Header Token is active, the Header Name field is pre-filled; the Header Value field shows the placeholder "••••••••" with no value
- [ ] AC-027: When IP Allowlisting is active, only the informational IP range panel and setup guide are shown — no credential fields are rendered
- [ ] AC-028: `GET /api/projects/{projectId}` never returns plaintext credential values; `basicAuthSecretUri`, `headerTokenName`, and `headerTokenSecretUri` are included but credential values are omitted from the response

---

### US-005: Read Access Credentials at Pipeline Runtime

**As a** worker pipeline stage (ExecutorRouter)
**I want to** retrieve the project's access credentials securely at test execution time
**So that** the HttpExecutor and PlaywrightExecutor can authenticate against the product URL without credentials being exposed in logs or Cosmos documents

#### Acceptance Criteria

- [ ] AC-029: The `IProjectAccessCredentialProvider` interface in `Testurio.Core` exposes a method to resolve credentials for a given `projectId`, returning a typed result: `IpAllowlist` (no credentials), `BasicAuth` (username + password), or `HeaderToken` (header name + value)
- [ ] AC-030: The concrete implementation in `Testurio.Infrastructure` reads the secret URI from the project document and retrieves the secret value from Azure Key Vault using Managed Identity at call time — no credentials are cached in memory beyond a single pipeline run
- [ ] AC-031: If Key Vault is unreachable or the secret URI is invalid, the credential provider throws a typed exception (`CredentialRetrievalException`) that the pipeline stage catches and reports as a `Failed` run with an appropriate error message
- [ ] AC-032: Credential retrieval is scoped to `userId` — the provider validates that the project document belongs to the requesting user's context before fetching any Key Vault secret
- [ ] AC-033: `HttpExecutor` applies Basic Auth credentials via the `Authorization: Basic <base64>` header on every request when `access_mode` is `basic_auth`
- [ ] AC-034: `PlaywrightExecutor` passes Basic Auth credentials via Playwright's `httpCredentials` option when `access_mode` is `basic_auth`
- [ ] AC-035: `HttpExecutor` injects the custom header on every request when `access_mode` is `header_token`
- [ ] AC-036: `PlaywrightExecutor` injects the custom header via `extraHTTPHeaders` on every request when `access_mode` is `header_token`
- [ ] AC-037: When `access_mode` is `ip_allowlist`, both executors make requests without any additional auth header; no credential lookup is performed

---

### US-006: Switch Between Access Methods

**As a** QA lead
**I want to** change the access method for a project at any time
**So that** I can adapt to infrastructure changes (e.g. moving from Basic Auth to IP allowlisting) without recreating the project

#### Acceptance Criteria

- [ ] AC-038: Changing the selected access mode in the UI hides the fields for the previous mode and shows the fields for the new mode immediately (no page reload required)
- [ ] AC-039: Saving the new access mode with valid credentials atomically: stores new Key Vault secrets (if any), updates the Cosmos document with new `access_mode` and new secret URIs, and removes or overwrites the old Key Vault secrets
- [ ] AC-040: If the Key Vault write fails during a mode switch, the Cosmos document is not updated and the previous configuration remains intact — the save is treated as failed and the UI shows an error
- [ ] AC-041: Switching to IP Allowlisting from any credential mode removes the credential fields from the UI and clears existing Key Vault secrets associated with the project
- [ ] AC-042: A mode switch does not affect test runs already in progress — the executor reads credentials at the start of each pipeline stage; in-flight runs use the snapshot retrieved at their start

---

### US-007: Validate API-Layer Access Configuration Updates

**As a** QA lead
**I want to** receive meaningful error responses when I submit invalid access configuration data directly to the API
**So that** integration tooling and the portal both handle errors consistently

#### Acceptance Criteria

- [ ] AC-043: `PATCH /api/projects/{projectId}/access` with `access_mode` set to an unrecognised value returns `400 Bad Request` with a `ValidationProblemDetails` body
- [ ] AC-044: `PATCH /api/projects/{projectId}/access` with `access_mode: "basic_auth"` and missing or empty `basicAuthUser` or `basicAuthPass` fields returns `400 Bad Request`
- [ ] AC-045: `PATCH /api/projects/{projectId}/access` with `access_mode: "header_token"` and missing or empty `headerTokenName` or `headerTokenValue` fields returns `400 Bad Request`
- [ ] AC-046: `PATCH /api/projects/{projectId}/access` with an invalid `headerTokenName` (contains spaces or special characters) returns `400 Bad Request` with a field-level error message
- [ ] AC-047: A request targeting a project that belongs to a different user returns `403 Forbidden`
- [ ] AC-048: A request targeting a non-existent or soft-deleted project returns `404 Not Found`
