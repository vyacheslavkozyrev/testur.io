import apiClient from '@/services/apiClient';
import type { DashboardResponse } from '@/types/dashboard.types';

export const dashboardService = {
  get: (): Promise<DashboardResponse> =>
    apiClient.get<DashboardResponse>('/v1/stats/dashboard').then((r) => r.data),
};
