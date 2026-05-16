import { http, HttpResponse } from 'msw';
import type { ProjectAccessDto } from '@/types/projectAccess.types';

const mockProjectAccess: ProjectAccessDto = {
  projectId: '00000000-0000-0000-0000-000000000001',
  accessMode: 'ipAllowlist',
  basicAuthUser: null,
  headerTokenName: null,
};

export const projectAccessHandlers = [
  http.get('/v1/projects/:projectId/access', ({ params }) => {
    if (params.projectId === mockProjectAccess.projectId) {
      return HttpResponse.json(mockProjectAccess);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.patch('/v1/projects/:projectId/access', async ({ params, request }) => {
    if (params.projectId !== mockProjectAccess.projectId) {
      return new HttpResponse(null, { status: 404 });
    }
    const body = await request.json() as Partial<typeof mockProjectAccess>;
    return HttpResponse.json({ ...mockProjectAccess, ...body });
  }),
];
