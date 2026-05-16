export interface ReportSettingsDto {
  reportTemplateUri: string | null;
  reportTemplateFileName: string | null;
  reportIncludeLogs: boolean;
  reportIncludeScreenshots: boolean;
}

export interface UpdateReportSettingsRequest {
  reportIncludeLogs: boolean;
  reportIncludeScreenshots: boolean;
}

export interface ReportTemplateUploadResponse {
  blobUri: string;
  warnings: string[];
}
