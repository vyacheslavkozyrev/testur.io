import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { projectApiAuthService } from '@/services/project/projectApiAuthService';
import type { ProjectApiAuthDto, UpdateProjectApiAuthRequest } from '@/types/projectApiAuth.types';
import type { ApiError } from '@/types/api.types';

export const PROJECT_API_AUTH_KEYS = {
  detail: (projectId: string) => ['projectApiAuth', projectId] as const,
};

export function useProjectApiAuth(projectId: string) {
  return useQuery<ProjectApiAuthDto, ApiError>({
    queryKey: PROJECT_API_AUTH_KEYS.detail(projectId),
    queryFn: () => projectApiAuthService.get(projectId),
    enabled: Boolean(projectId),
  });
}

export function useUpdateProjectApiAuth(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation<ProjectApiAuthDto, ApiError, UpdateProjectApiAuthRequest>({
    mutationFn: (body) => projectApiAuthService.update(projectId, body),
    onSuccess: (updated) => {
      queryClient.setQueryData(PROJECT_API_AUTH_KEYS.detail(projectId), updated);
    },
  });
}
