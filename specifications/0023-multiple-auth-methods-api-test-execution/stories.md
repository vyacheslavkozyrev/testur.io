# User Stories — Multiple Authentication Methods for API Test Execution (0023)

## Out of Scope

The following are explicitly **not** part of this feature:

- OAuth 2.0 / OpenID Connect token flows — no client-credentials or authorization-code grant; Bearer token is supplied as a static pre-obtained value
- Per-scenario or per-step authentication override — the auth method is configured at the project level and applied uniformly to every request in a run
- Authentication for UI E2E (Playwright) test execution — `PlaywrightExecutor` is not affected; this feature governs `HttpExecutor` only
- Mutual TLS (mTLS) client certificate authentication — not supported in MVP
- Digest authentication — not supported in MVP
- Dynamic credential rotation or OAuth token refresh — credentials are static values stored in Key Vault; no automatic refresh cycle
- Testing or validating the configured credentials against the product URL at save time — credentials are accepted as-is; execution failures surface in the run report
- Per-request credential selection — one auth method is active per project at a time
- Storing credentials in Cosmos DB — plaintext credentials are stored exclusively in Azure Key Vault; only Key Vault secret URIs are persisted in the project document

---

## Stories

### US-001: Configure Bearer Token Authentication

**As a** QA lead
**I want to** configure a Bearer token for my project's API test execution
**So that** the test executor can authenticate against endpoints that require a JWT or opaque token in the Authorization header without me manually editing each request

#### Acceptance Criteria

- [ ] AC-001: The project settings page's API Authentication section presents a choice of four authentication methods: "None", "Bearer Token", "API Key", and "HTTP Basic Auth"
- [ ] AC-002: Selecting "Bearer Token" reveals a single required "Token" field (masked input)
- [ ] AC-003: The Token field must be non-empty; submitting with it empty shows an inline validation error and the form is not submitted
- [ ] AC-004: On save, the token value is stored as a secret in Azure Key Vault under `projects/{projectId}/api-auth-bearer-token`; only the Key Vault secret URI is persisted in the Cosmos DB project document (`apiAuthBearerTokenSecretUri`)
- [ ] AC-005: The token value is never exposed in API responses, never written to logs, and never included in error messages
- [ ] AC-006: After saving, the Token field renders empty with a placeholder ("••••••••") to indicate a value is stored without revealing it
- [ ] AC-007: `HttpExecutor` reads the token from Key Vault at the start of each run and injects `Authorization: Bearer <token>` on every HTTP request in the run
- [ ] AC-008: The API returns `200 OK` with the updated project document (containing only the secret URI, not the plaintext token) on successful save
- [ ] AC-009: Switching away from "Bearer Token" to another method clears the Key Vault secret and removes the secret URI from the Cosmos document

---

### US-002: Configure API Key Authentication (Header or Query Parameter)

**As a** QA lead
**I want to** configure an API key delivered either as a custom header or as a query parameter
**So that** the test executor can authenticate against endpoints that expect an API key without requiring a Bearer token format

#### Acceptance Criteria

- [ ] AC-010: Selecting "API Key" reveals three required fields: "Key Name" (plain text), "Key Value" (masked input), and a placement selector with two options — "Header" and "Query Parameter"
- [ ] AC-011: All three fields must be non-empty / selected; submitting with any field empty or unselected shows an inline validation error and the form is not submitted
- [ ] AC-012: On save, the Key Value is stored as a secret in Azure Key Vault under `projects/{projectId}/api-auth-api-key-value`; only the Key Vault secret URI is persisted in the Cosmos document; `apiAuthApiKeyName` and `apiAuthApiKeyPlacement` (`header` | `query`) are persisted as plaintext fields
- [ ] AC-013: The Key Value is never exposed in API responses, never written to logs, and never included in error messages
- [ ] AC-014: After saving, the Key Value field renders empty with a placeholder ("••••••••") to indicate a value is stored
- [ ] AC-015: When placement is "Header", `HttpExecutor` injects the key as `{KeyName}: {KeyValue}` on every HTTP request
- [ ] AC-016: When placement is "Query Parameter", `HttpExecutor` appends `?{KeyName}={KeyValue}` (or `&{KeyName}={KeyValue}` if the URL already contains query parameters) to every request URL
- [ ] AC-017: The API returns `200 OK` with the updated project document on successful save
- [ ] AC-018: Switching away from "API Key" to another method clears the Key Vault secret and removes the secret URI and plaintext key fields from the Cosmos document

---

### US-003: Configure HTTP Basic Auth for API Test Execution

**As a** QA lead
**I want to** configure an HTTP Basic Auth username and password specifically for API test execution
**So that** `HttpExecutor` can authenticate against Basic Auth-protected API endpoints independently of the environment access credentials configured in feature 0017

#### Acceptance Criteria

- [ ] AC-019: Selecting "HTTP Basic Auth" (in the API Authentication section) reveals two required fields: "Username" (plain text) and "Password" (masked input)
- [ ] AC-020: Both fields must be non-empty; submitting with either empty shows an inline validation error and the form is not submitted
- [ ] AC-021: On save, the password is stored as a secret in Azure Key Vault under `projects/{projectId}/api-auth-basic-password`; only the Key Vault secret URI is persisted in the Cosmos document (`apiAuthBasicPasswordSecretUri`); `apiAuthBasicUsername` is stored as a plaintext field
- [ ] AC-022: The password is never exposed in API responses, never written to logs, and never included in error messages
- [ ] AC-023: After saving, the Password field renders empty with a placeholder ("••••••••") and the Username field is pre-filled with the stored value
- [ ] AC-024: `HttpExecutor` constructs a Base64-encoded `Authorization: Basic {credentials}` header from the username and password retrieved from Key Vault and injects it on every HTTP request in the run
- [ ] AC-025: The API returns `200 OK` with the updated project document on successful save
- [ ] AC-026: Switching away from "HTTP Basic Auth" to another method clears the Key Vault secret and removes the secret URI and username field from the Cosmos document

---

### US-004: Select "None" to Send Unauthenticated Requests

**As a** QA lead
**I want to** explicitly select "None" as the authentication method for my project
**So that** the test executor sends requests without any authentication header when testing public or internally accessible endpoints

#### Acceptance Criteria

- [ ] AC-027: Selecting "None" shows no credential fields and requires no additional input
- [ ] AC-028: Saving with "None" persists `apiAuthMethod: "none"` on the project document and removes any previously stored Key Vault secrets and secret URIs
- [ ] AC-029: `HttpExecutor` sends requests with no authentication header when `apiAuthMethod` is `"none"`
- [ ] AC-030: When a project is created (feature 0006) and no `apiAuthMethod` is supplied, `apiAuthMethod` defaults to `"none"`
- [ ] AC-031: The API returns `200 OK` with the updated project document on successful save

---

### US-005: View the Current Authentication Method

**As a** QA lead
**I want to** see which authentication method is currently active when I open the project settings page
**So that** I can verify or update the setting without losing my existing configuration

#### Acceptance Criteria

- [ ] AC-032: `GET /api/projects/{projectId}` includes `apiAuthMethod` (`"none"` | `"bearer"` | `"api_key"` | `"basic"`) in the response body
- [ ] AC-033: For "Bearer Token" method, the response includes `apiAuthBearerTokenConfigured: true` (a boolean) instead of the token value
- [ ] AC-034: For "API Key" method, the response includes `apiAuthApiKeyName` (plaintext), `apiAuthApiKeyPlacement` (`"header"` | `"query"`), and `apiAuthApiKeyValueConfigured: true`
- [ ] AC-035: For "HTTP Basic Auth" method, the response includes `apiAuthBasicUsername` (plaintext) and `apiAuthBasicPasswordConfigured: true`
- [ ] AC-036: For "None" method (or projects created before this feature where the field is absent), the response includes `apiAuthMethod: "none"` and no credential fields
- [ ] AC-037: Opening the project settings page pre-selects the active authentication method and pre-fills all non-secret fields (Key Name, placement, Username) with the stored values

---

### US-006: Validate API-Layer Authentication Configuration Updates

**As a** QA lead
**I want to** receive meaningful error responses when submitting invalid authentication configuration directly to the API
**So that** the portal and any integration tooling handle errors consistently

#### Acceptance Criteria

- [ ] AC-038: `PATCH /api/projects/{projectId}` (or the relevant project update endpoint) with `apiAuthMethod` set to an unrecognised value returns `400 Bad Request` with a `ValidationProblemDetails` body identifying the field
- [ ] AC-039: Submitting `apiAuthMethod: "bearer"` without supplying a token value returns `400 Bad Request`
- [ ] AC-040: Submitting `apiAuthMethod: "api_key"` without supplying key name, key value, or placement returns `400 Bad Request` identifying the missing fields
- [ ] AC-041: Submitting `apiAuthMethod: "basic"` without supplying username or password returns `400 Bad Request` identifying the missing fields
- [ ] AC-042: A request targeting a project that belongs to a different user returns `403 Forbidden`
- [ ] AC-043: A request targeting a non-existent or soft-deleted project returns `404 Not Found`
