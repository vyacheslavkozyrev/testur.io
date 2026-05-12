import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { pmToolService } from '@/services/pmTool/pmToolService';
import type {
  PMToolConnectionResponse,
  SaveADOConnectionRequest,
  SaveJiraConnectionRequest,
  TestConnectionResult,
  WebhookSetupInfo,
  UpdateTokenRequest,
} from '@/types/pmTool.types';
import type { ApiError } from '@/types/api.types';

export const PM_TOOL_KEYS = {
  status: (projectId: string) => ['projects', projectId, 'integrations'] as const,
  webhookSetup: (projectId: string) => ['projects', projectId, 'integrations', 'webhook-setup'] as const,
};

export function useIntegrationStatus(projectId: string) {
  return useQuery<PMToolConnectionResponse, ApiError>({
    queryKey: PM_TOOL_KEYS.status(projectId),
    queryFn: () => pmToolService.getIntegrationStatus(projectId),
    enabled: Boolean(projectId),
  });
}

export function useWebhookSetup(projectId: string, enabled: boolean) {
  return useQuery<WebhookSetupInfo, ApiError>({
    queryKey: PM_TOOL_KEYS.webhookSetup(projectId),
    queryFn: () => pmToolService.getWebhookSetup(projectId),
    enabled: Boolean(projectId) && enabled,
  });
}

export function useSaveADOConnection(projectId: string) {
  const qc = useQueryClient();
  return useMutation<PMToolConnectionResponse, ApiError, SaveADOConnectionRequest>({
    mutationFn: (body) => pmToolService.saveADOConnection(projectId, body),
    onSuccess: (data) => {
      qc.setQueryData(PM_TOOL_KEYS.status(projectId), data);
      // Invalidate webhook setup so it re-fetches fresh secret state.
      qc.invalidateQueries({ queryKey: PM_TOOL_KEYS.webhookSetup(projectId) });
    },
  });
}

export function useSaveJiraConnection(projectId: string) {
  const qc = useQueryClient();
  return useMutation<PMToolConnectionResponse, ApiError, SaveJiraConnectionRequest>({
    mutationFn: (body) => pmToolService.saveJiraConnection(projectId, body),
    onSuccess: (data) => {
      qc.setQueryData(PM_TOOL_KEYS.status(projectId), data);
      qc.invalidateQueries({ queryKey: PM_TOOL_KEYS.webhookSetup(projectId) });
    },
  });
}

export function useTestConnection(projectId: string) {
  return useMutation<TestConnectionResult, ApiError>({
    mutationFn: () => pmToolService.testConnection(projectId),
  });
}

export function useRemoveConnection(projectId: string) {
  const qc = useQueryClient();
  return useMutation<PMToolConnectionResponse, ApiError>({
    mutationFn: () => pmToolService.removeConnection(projectId),
    onSuccess: (data) => {
      qc.setQueryData(PM_TOOL_KEYS.status(projectId), data);
      qc.removeQueries({ queryKey: PM_TOOL_KEYS.webhookSetup(projectId) });
    },
  });
}

export function useRegenerateWebhookSecret(projectId: string) {
  const qc = useQueryClient();
  return useMutation<WebhookSetupInfo, ApiError>({
    mutationFn: () => pmToolService.regenerateWebhookSecret(projectId),
    onSuccess: (data) => {
      qc.setQueryData(PM_TOOL_KEYS.webhookSetup(projectId), data);
    },
  });
}

export function useUpdateToken(projectId: string) {
  const qc = useQueryClient();
  return useMutation<PMToolConnectionResponse, ApiError, UpdateTokenRequest>({
    mutationFn: (body) => pmToolService.updateToken(projectId, body),
    onSuccess: (data) => {
      qc.setQueryData(PM_TOOL_KEYS.status(projectId), data);
    },
  });
}
