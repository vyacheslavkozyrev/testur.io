import { http, HttpResponse } from 'msw';
import type { ProjectApiAuthDto, UpdateProjectApiAuthRequest } from '@/types/projectApiAuth.types';

const mockProjectApiAuth: ProjectApiAuthDto = {
  projectId: '00000000-0000-0000-0000-000000000001',
  apiAuthMethod: 'none',
  apiAuthBearerTokenConfigured: null,
  apiAuthApiKeyName: null,
  apiAuthApiKeyPlacement: null,
  apiAuthApiKeyValueConfigured: null,
  apiAuthBasicUsername: null,
  apiAuthBasicPasswordConfigured: null,
};

export const projectApiAuthHandlers = [
  http.get('/v1/projects/:projectId/api-auth', ({ params }) => {
    if (params.projectId === mockProjectApiAuth.projectId) {
      return HttpResponse.json(mockProjectApiAuth);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.patch('/v1/projects/:projectId/api-auth', async ({ params, request }) => {
    if (params.projectId !== mockProjectApiAuth.projectId) {
      return new HttpResponse(null, { status: 404 });
    }
    const body = (await request.json()) as UpdateProjectApiAuthRequest;
    const updated: ProjectApiAuthDto = {
      ...mockProjectApiAuth,
      apiAuthMethod: body.apiAuthMethod,
      apiAuthBearerTokenConfigured: body.apiAuthMethod === 'bearer' ? true : null,
      apiAuthApiKeyName: body.apiAuthMethod === 'api_key' ? (body.apiAuthApiKeyName ?? null) : null,
      apiAuthApiKeyPlacement: body.apiAuthMethod === 'api_key' ? (body.apiAuthApiKeyPlacement ?? null) : null,
      apiAuthApiKeyValueConfigured: body.apiAuthMethod === 'api_key' ? true : null,
      apiAuthBasicUsername: body.apiAuthMethod === 'basic' ? (body.apiAuthBasicUsername ?? null) : null,
      apiAuthBasicPasswordConfigured: body.apiAuthMethod === 'basic' ? true : null,
    };
    return HttpResponse.json(updated);
  }),
];
