import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { projectAccessService } from '@/services/project/projectAccessService';
import type { ProjectAccessDto, UpdateProjectAccessRequest } from '@/types/projectAccess.types';
import type { ApiError } from '@/types/api.types';

export const PROJECT_ACCESS_KEYS = {
  detail: (projectId: string) => ['projectAccess', projectId] as const,
};

export function useProjectAccess(projectId: string) {
  return useQuery<ProjectAccessDto, ApiError>({
    queryKey: PROJECT_ACCESS_KEYS.detail(projectId),
    queryFn: () => projectAccessService.get(projectId),
    enabled: Boolean(projectId),
  });
}

export function useUpdateProjectAccess(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation<ProjectAccessDto, ApiError, UpdateProjectAccessRequest>({
    mutationFn: (body) => projectAccessService.update(projectId, body),
    onSuccess: (updated) => {
      queryClient.setQueryData(PROJECT_ACCESS_KEYS.detail(projectId), updated);
    },
  });
}
