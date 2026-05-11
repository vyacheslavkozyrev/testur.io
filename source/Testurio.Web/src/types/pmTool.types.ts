export type PMToolType = 'ado' | 'jira';

export type ADOAuthMethod = 'pat' | 'oAuth';

export type JiraAuthMethod = 'apiToken' | 'pat';

export type IntegrationStatus = 'none' | 'active' | 'authError';

export type TestConnectionStatus = 'ok' | 'auth_error' | 'unreachable';

export interface ADOConnection {
  orgUrl: string;
  projectName: string;
  team: string;
  inTestingStatus: string;
  authMethod: ADOAuthMethod;
  /** PAT value — only sent when saving; never returned from the API */
  pat?: string;
  /** OAuth token — only sent when saving; never returned from the API */
  oAuthToken?: string;
}

export interface JiraConnection {
  baseUrl: string;
  projectKey: string;
  inTestingStatus: string;
  authMethod: JiraAuthMethod;
  /** Email — only sent when saving; never returned from the API */
  email?: string;
  /** API token — only sent when saving; never returned from the API */
  apiToken?: string;
  /** PAT — only sent when saving; never returned from the API */
  pat?: string;
}

/** Response body from GET /v1/projects/{id}/integrations */
export interface PMToolConnectionResponse {
  pmTool: PMToolType | null;
  integrationStatus: IntegrationStatus;

  // ADO
  adoOrgUrl: string | null;
  adoProjectName: string | null;
  adoTeam: string | null;
  adoInTestingStatus: string | null;
  adoAuthMethod: ADOAuthMethod | null;
  adoTokenSecretUri: string | null;

  // Jira
  jiraBaseUrl: string | null;
  jiraProjectKey: string | null;
  jiraInTestingStatus: string | null;
  jiraAuthMethod: JiraAuthMethod | null;
  jiraApiTokenSecretUri: string | null;
  jiraEmailSecretUri: string | null;
  jiraPatSecretUri: string | null;
}

/** Response body from POST /v1/projects/{id}/integrations/test-connection */
export interface TestConnectionResult {
  status: TestConnectionStatus;
  message: string;
}

/** Response body from GET /v1/projects/{id}/integrations/webhook-setup */
export interface WebhookSetupInfo {
  webhookUrl: string;
  webhookSecret: string;
  isMasked: boolean;
}

export interface SaveADOConnectionRequest {
  orgUrl: string;
  projectName: string;
  team: string;
  inTestingStatus: string;
  authMethod: ADOAuthMethod;
  pat?: string;
  oAuthToken?: string;
}

export interface SaveJiraConnectionRequest {
  baseUrl: string;
  projectKey: string;
  inTestingStatus: string;
  authMethod: JiraAuthMethod;
  email?: string;
  apiToken?: string;
  pat?: string;
}

export interface UpdateTokenRequest {
  token: string;
  email?: string;
}
