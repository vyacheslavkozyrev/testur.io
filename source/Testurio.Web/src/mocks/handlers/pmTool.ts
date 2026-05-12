import { http, HttpResponse } from 'msw';
import type {
  PMToolConnectionResponse,
  TestConnectionResult,
  WebhookSetupInfo,
} from '@/types/pmTool.types';

const mockAdoIntegration: PMToolConnectionResponse = {
  pmTool: 'ado',
  integrationStatus: 'active',
  adoOrgUrl: 'https://dev.azure.com/my-org',
  adoProjectName: 'My Project',
  adoTeam: 'My Team',
  adoInTestingStatus: 'In Testing',
  adoAuthMethod: 'pat',
  adoTokenSecretUri: 'projects--proj-001--adoToken',
  jiraBaseUrl: null,
  jiraProjectKey: null,
  jiraInTestingStatus: null,
  jiraAuthMethod: null,
  jiraApiTokenSecretUri: null,
  jiraEmailSecretUri: null,
  jiraPatSecretUri: null,
};

const mockNoIntegration: PMToolConnectionResponse = {
  pmTool: null,
  integrationStatus: 'none',
  adoOrgUrl: null,
  adoProjectName: null,
  adoTeam: null,
  adoInTestingStatus: null,
  adoAuthMethod: null,
  adoTokenSecretUri: null,
  jiraBaseUrl: null,
  jiraProjectKey: null,
  jiraInTestingStatus: null,
  jiraAuthMethod: null,
  jiraApiTokenSecretUri: null,
  jiraEmailSecretUri: null,
  jiraPatSecretUri: null,
};

const mockWebhookSetup: WebhookSetupInfo = {
  webhookUrl: 'https://api.testur.io/webhooks/ado',
  webhookSecret: 'abc123secret',
  isMasked: false,
};

const mockTestSuccess: TestConnectionResult = {
  status: 'ok',
  message: 'Connection successful.',
};

export const pmToolHandlers = [
  http.get('/v1/projects/:projectId/integrations', () =>
    HttpResponse.json(mockAdoIntegration),
  ),

  http.post('/v1/projects/:projectId/integrations/ado', () =>
    HttpResponse.json(mockAdoIntegration),
  ),

  http.post('/v1/projects/:projectId/integrations/jira', () => {
    const jiraResponse: PMToolConnectionResponse = {
      pmTool: 'jira',
      integrationStatus: 'active',
      adoOrgUrl: null,
      adoProjectName: null,
      adoTeam: null,
      adoInTestingStatus: null,
      adoAuthMethod: null,
      adoTokenSecretUri: null,
      jiraBaseUrl: 'https://my-org.atlassian.net',
      jiraProjectKey: 'PROJ',
      jiraInTestingStatus: 'In Testing',
      jiraAuthMethod: 'apiToken',
      jiraApiTokenSecretUri: 'projects--proj-001--jiraApiToken',
      jiraEmailSecretUri: 'projects--proj-001--jiraEmail',
      jiraPatSecretUri: null,
    };
    return HttpResponse.json(jiraResponse);
  }),

  http.delete('/v1/projects/:projectId/integrations', () =>
    HttpResponse.json(mockNoIntegration),
  ),

  http.post('/v1/projects/:projectId/integrations/test-connection', () =>
    HttpResponse.json(mockTestSuccess),
  ),

  http.get('/v1/projects/:projectId/integrations/webhook-setup', () =>
    HttpResponse.json(mockWebhookSetup),
  ),

  http.post('/v1/projects/:projectId/integrations/webhook-setup/regenerate', () => {
    const regenerated: WebhookSetupInfo = {
      webhookUrl: 'https://api.testur.io/webhooks/ado',
      webhookSecret: 'newregeneratedsecret456',
      isMasked: false,
    };
    return HttpResponse.json(regenerated);
  }),

  http.patch('/v1/projects/:projectId/integrations/token', () =>
    HttpResponse.json({ ...mockAdoIntegration, integrationStatus: 'active' }),
  ),
];
