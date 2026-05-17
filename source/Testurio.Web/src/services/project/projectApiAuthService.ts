import apiClient from '@/services/apiClient';
import type { ProjectApiAuthDto, UpdateProjectApiAuthRequest } from '@/types/projectApiAuth.types';

export const projectApiAuthService = {
  get: (projectId: string): Promise<ProjectApiAuthDto> =>
    apiClient.get<ProjectApiAuthDto>(`/v1/projects/${projectId}/api-auth`).then((r) => r.data),

  update: (projectId: string, body: UpdateProjectApiAuthRequest): Promise<ProjectApiAuthDto> =>
    apiClient.patch<ProjectApiAuthDto>(`/v1/projects/${projectId}/api-auth`, body).then((r) => r.data),
};
