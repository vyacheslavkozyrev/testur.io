import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { projectService } from '@/services/project/projectService';
import type {
  ProjectDto,
  CreateProjectRequest,
  UpdateProjectRequest,
  PromptCheckRequest,
  PromptCheckFeedback,
} from '@/types/project.types';
import type { ApiError } from '@/types/api.types';

export const PROJECT_KEYS = {
  all: ['projects'] as const,
  detail: (id: string) => ['projects', id] as const,
};

export function useProjects() {
  return useQuery<ProjectDto[], ApiError>({
    queryKey: PROJECT_KEYS.all,
    queryFn: projectService.list,
  });
}

export function useProject(id: string) {
  return useQuery<ProjectDto, ApiError>({
    queryKey: PROJECT_KEYS.detail(id),
    queryFn: () => projectService.get(id),
    enabled: Boolean(id),
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation<ProjectDto, ApiError, CreateProjectRequest>({
    mutationFn: projectService.create,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: PROJECT_KEYS.all }),
  });
}

export function useUpdateProject(id: string) {
  const queryClient = useQueryClient();
  return useMutation<ProjectDto, ApiError, UpdateProjectRequest>({
    mutationFn: (body) => projectService.update(id, body),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: PROJECT_KEYS.all });
      queryClient.setQueryData(PROJECT_KEYS.detail(id), updated);
    },
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: projectService.delete,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: PROJECT_KEYS.all }),
  });
}

export function usePromptCheck(projectId: string) {
  return useMutation<PromptCheckFeedback, ApiError, PromptCheckRequest>({
    mutationFn: (body) => projectService.promptCheck(projectId, body),
  });
}
