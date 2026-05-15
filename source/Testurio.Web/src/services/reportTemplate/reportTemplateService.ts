import apiClient from '@/services/apiClient';
import type {
  ReportSettingsDto,
  UpdateReportSettingsRequest,
  ReportTemplateUploadResponse,
} from '@/types/reportSettings.types';

export const reportTemplateService = {
  getSettings: (projectId: string): Promise<ReportSettingsDto> =>
    apiClient
      .get<ReportSettingsDto>(`/v1/projects/${projectId}/report-settings`)
      .then((r) => r.data),

  uploadTemplate: (
    projectId: string,
    file: File,
  ): Promise<ReportTemplateUploadResponse> => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient
      .post<ReportTemplateUploadResponse>(
        `/v1/projects/${projectId}/report-settings/template`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      .then((r) => r.data);
  },

  removeTemplate: (projectId: string): Promise<void> =>
    apiClient
      .delete(`/v1/projects/${projectId}/report-settings/template`)
      .then(() => undefined),

  updateSettings: (
    projectId: string,
    body: UpdateReportSettingsRequest,
  ): Promise<ReportSettingsDto> =>
    apiClient
      .patch<ReportSettingsDto>(`/v1/projects/${projectId}/report-settings`, body)
      .then((r) => r.data),
};
