import { useQuery } from '@tanstack/react-query';
import { dashboardService } from '@/services/dashboard/dashboardService';
import type { DashboardResponse } from '@/types/dashboard.types';
import type { ApiError } from '@/types/api.types';

export const DASHBOARD_KEYS = {
  all: ['dashboard'] as const,
};

export function useDashboard() {
  return useQuery<DashboardResponse, ApiError>({
    queryKey: DASHBOARD_KEYS.all,
    queryFn: dashboardService.get,
  });
}
