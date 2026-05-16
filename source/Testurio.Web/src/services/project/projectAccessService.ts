import apiClient from '@/services/apiClient';
import type { ProjectAccessDto, UpdateProjectAccessRequest } from '@/types/projectAccess.types';

export const projectAccessService = {
  get: (projectId: string): Promise<ProjectAccessDto> =>
    apiClient.get<ProjectAccessDto>(`/v1/projects/${projectId}/access`).then((r) => r.data),

  update: (projectId: string, body: UpdateProjectAccessRequest): Promise<ProjectAccessDto> =>
    apiClient.patch<ProjectAccessDto>(`/v1/projects/${projectId}/access`, body).then((r) => r.data),
};
