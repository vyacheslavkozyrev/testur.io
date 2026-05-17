import apiClient from '@/services/apiClient';
import type { ProjectHistoryResponse, RunDetailResponse } from '@/types/history.types';

export const historyService = {
  getProjectHistory: (projectId: string): Promise<ProjectHistoryResponse> =>
    apiClient
      .get<ProjectHistoryResponse>(`/v1/stats/projects/${projectId}/history`)
      .then((r) => r.data),

  getRunDetail: (projectId: string, runId: string): Promise<RunDetailResponse> =>
    apiClient
      .get<RunDetailResponse>(`/v1/stats/projects/${projectId}/runs/${runId}`)
      .then((r) => r.data),
};
