import { http, HttpResponse } from 'msw';
import type { ProjectDto, PromptCheckFeedback } from '@/types/project.types';

const mockProject: ProjectDto = {
  projectId: '00000000-0000-0000-0000-000000000001',
  name: 'Demo Project',
  productUrl: 'https://example.com',
  testingStrategy: 'Focus on API contracts and key user flows.',
  customPrompt: null,
  allowedWorkItemTypes: null,
  requestTimeoutSeconds: 30,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
};

const mockPromptCheckFeedback: PromptCheckFeedback = {
  clarity: {
    assessment: 'The prompt is clear and concise.',
    suggestion: null,
  },
  specificity: {
    assessment: 'The prompt is specific enough for API testing.',
    suggestion: 'Consider specifying the authentication method expected.',
  },
  potentialConflicts: {
    assessment: 'No conflicts detected with the current testing strategy.',
    suggestion: null,
  },
};

export const projectHandlers = [
  http.get('/v1/projects', () => HttpResponse.json([mockProject])),

  http.get('/v1/projects/:projectId', ({ params }) => {
    if (params.projectId === mockProject.projectId) {
      return HttpResponse.json(mockProject);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.post('/v1/projects', () => HttpResponse.json(mockProject, { status: 201 })),

  http.put('/v1/projects/:projectId', ({ params }) => {
    if (params.projectId === mockProject.projectId) {
      return HttpResponse.json(mockProject);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.delete('/v1/projects/:projectId', ({ params }) => {
    if (params.projectId === mockProject.projectId) {
      return new HttpResponse(null, { status: 204 });
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.post('/v1/projects/:projectId/prompt-check', ({ params }) => {
    if (params.projectId === mockProject.projectId) {
      return HttpResponse.json(mockPromptCheckFeedback);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.patch('/v1/projects/:projectId/work-item-type-filter', ({ params }) => {
    if (params.projectId === mockProject.projectId) {
      return HttpResponse.json({ ...mockProject, allowedWorkItemTypes: ['Story', 'Bug'] });
    }
    return new HttpResponse(null, { status: 404 });
  }),
];
