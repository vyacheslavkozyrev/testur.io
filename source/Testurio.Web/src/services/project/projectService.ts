import apiClient from '@/services/apiClient';
import type {
  ProjectDto,
  CreateProjectRequest,
  UpdateProjectRequest,
  PromptCheckRequest,
  PromptCheckFeedback,
} from '@/types/project.types';

export const projectService = {
  list: (): Promise<ProjectDto[]> =>
    apiClient.get<ProjectDto[]>('/v1/projects').then((r) => r.data),

  get: (id: string): Promise<ProjectDto> =>
    apiClient.get<ProjectDto>(`/v1/projects/${id}`).then((r) => r.data),

  create: (body: CreateProjectRequest): Promise<ProjectDto> =>
    apiClient.post<ProjectDto>('/v1/projects', body).then((r) => r.data),

  update: (id: string, body: UpdateProjectRequest): Promise<ProjectDto> =>
    apiClient.put<ProjectDto>(`/v1/projects/${id}`, body).then((r) => r.data),

  delete: (id: string): Promise<void> =>
    apiClient.delete(`/v1/projects/${id}`).then(() => undefined),

  promptCheck: (id: string, body: PromptCheckRequest): Promise<PromptCheckFeedback> =>
    apiClient.post<PromptCheckFeedback>(`/v1/projects/${id}/prompt-check`, body).then((r) => r.data),
};
