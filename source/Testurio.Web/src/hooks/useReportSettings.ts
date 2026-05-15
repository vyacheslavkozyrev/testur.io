import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { reportTemplateService } from '@/services/reportTemplate/reportTemplateService';
import type {
  ReportSettingsDto,
  UpdateReportSettingsRequest,
  ReportTemplateUploadResponse,
} from '@/types/reportSettings.types';
import type { ApiError } from '@/types/api.types';

export const REPORT_SETTINGS_KEYS = {
  settings: (projectId: string) => ['reportSettings', projectId] as const,
};

export function useReportSettings(projectId: string) {
  return useQuery<ReportSettingsDto, ApiError>({
    queryKey: REPORT_SETTINGS_KEYS.settings(projectId),
    queryFn: () => reportTemplateService.getSettings(projectId),
    enabled: Boolean(projectId),
  });
}

export function useUploadReportTemplate(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation<ReportTemplateUploadResponse, ApiError, File>({
    mutationFn: (file) => reportTemplateService.uploadTemplate(projectId, file),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: REPORT_SETTINGS_KEYS.settings(projectId),
      }),
  });
}

export function useRemoveReportTemplate(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, void>({
    mutationFn: () => reportTemplateService.removeTemplate(projectId),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: REPORT_SETTINGS_KEYS.settings(projectId),
      }),
  });
}

export function useUpdateReportSettings(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation<ReportSettingsDto, ApiError, UpdateReportSettingsRequest>({
    mutationFn: (body) => reportTemplateService.updateSettings(projectId, body),
    onSuccess: (updated) =>
      queryClient.setQueryData(REPORT_SETTINGS_KEYS.settings(projectId), updated),
  });
}
