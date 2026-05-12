import apiClient from '@/services/apiClient';
import type {
  PMToolConnectionResponse,
  SaveADOConnectionRequest,
  SaveJiraConnectionRequest,
  TestConnectionResult,
  WebhookSetupInfo,
  UpdateTokenRequest,
} from '@/types/pmTool.types';

const baseUrl = (projectId: string) => `/v1/projects/${projectId}/integrations`;

export const pmToolService = {
  getIntegrationStatus: (projectId: string): Promise<PMToolConnectionResponse> =>
    apiClient.get<PMToolConnectionResponse>(baseUrl(projectId)).then((r) => r.data),

  saveADOConnection: (
    projectId: string,
    body: SaveADOConnectionRequest,
  ): Promise<PMToolConnectionResponse> =>
    apiClient
      .post<PMToolConnectionResponse>(`${baseUrl(projectId)}/ado`, body)
      .then((r) => r.data),

  saveJiraConnection: (
    projectId: string,
    body: SaveJiraConnectionRequest,
  ): Promise<PMToolConnectionResponse> =>
    apiClient
      .post<PMToolConnectionResponse>(`${baseUrl(projectId)}/jira`, body)
      .then((r) => r.data),

  testConnection: (projectId: string): Promise<TestConnectionResult> =>
    apiClient
      .post<TestConnectionResult>(`${baseUrl(projectId)}/test-connection`)
      .then((r) => r.data),

  removeConnection: (projectId: string): Promise<PMToolConnectionResponse> =>
    apiClient.delete<PMToolConnectionResponse>(baseUrl(projectId)).then((r) => r.data),

  getWebhookSetup: (projectId: string): Promise<WebhookSetupInfo> =>
    apiClient
      .get<WebhookSetupInfo>(`${baseUrl(projectId)}/webhook-setup`)
      .then((r) => r.data),

  regenerateWebhookSecret: (projectId: string): Promise<WebhookSetupInfo> =>
    apiClient
      .post<WebhookSetupInfo>(`${baseUrl(projectId)}/webhook-setup/regenerate`)
      .then((r) => r.data),

  updateToken: (projectId: string, body: UpdateTokenRequest): Promise<PMToolConnectionResponse> =>
    apiClient
      .patch<PMToolConnectionResponse>(`${baseUrl(projectId)}/token`, body)
      .then((r) => r.data),
};
