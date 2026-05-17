import { useQuery } from '@tanstack/react-query';
import { historyService } from '@/services/history/historyService';
import type { ProjectHistoryResponse, RunDetailResponse } from '@/types/history.types';
import type { ApiError } from '@/types/api.types';

export const HISTORY_KEYS = {
  project: (projectId: string) => ['history', projectId] as const,
  run: (projectId: string, runId: string) =>
    ['history', projectId, 'run', runId] as const,
};

export function useProjectHistory(projectId: string) {
  return useQuery<ProjectHistoryResponse, ApiError>({
    queryKey: HISTORY_KEYS.project(projectId),
    queryFn: () => historyService.getProjectHistory(projectId),
    enabled: Boolean(projectId),
  });
}

export function useRunDetail(projectId: string, runId: string | null) {
  return useQuery<RunDetailResponse, ApiError>({
    queryKey: HISTORY_KEYS.run(projectId, runId ?? ''),
    queryFn: () => historyService.getRunDetail(projectId, runId!),
    enabled: Boolean(runId),
  });
}
