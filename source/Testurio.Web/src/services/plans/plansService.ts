import apiClient from '@/services/apiClient';
import type { PlanDefinition } from '@/types/plan.types';

export const plansService = {
  list: (): Promise<PlanDefinition[]> =>
    apiClient.get<PlanDefinition[]>('/v1/plans').then((r) => r.data),
};
