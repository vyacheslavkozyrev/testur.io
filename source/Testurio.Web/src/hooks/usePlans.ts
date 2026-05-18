import { useQuery } from '@tanstack/react-query';
import { plansService } from '@/services/plans/plansService';
import type { PlanDefinition } from '@/types/plan.types';
import type { ApiError } from '@/types/api.types';

export const PLANS_KEYS = {
  all: ['plans'] as const,
};

/**
 * Fetches the static plan catalog from GET /v1/plans.
 * staleTime matches the server Cache-Control max-age (3 600 s) so the plan
 * grid never flickers between navigations within the same browser session.
 */
export function usePlans() {
  return useQuery<PlanDefinition[], ApiError>({
    queryKey: PLANS_KEYS.all,
    queryFn: plansService.list,
    staleTime: 60 * 60 * 1000, // 1 hour — matches server max-age=3600
  });
}
