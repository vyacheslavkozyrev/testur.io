import { http, HttpResponse } from 'msw';
import type {
  ReportSettingsDto,
  ReportTemplateUploadResponse,
} from '@/types/reportSettings.types';

const mockReportSettings: ReportSettingsDto = {
  reportTemplateUri: null,
  reportTemplateFileName: null,
  reportIncludeLogs: true,
  reportIncludeScreenshots: true,
};

const mockUploadResponse: ReportTemplateUploadResponse = {
  blobUri: 'https://storage.example.com/templates/proj-001/report.md',
  warnings: [],
};

export const reportSettingsHandlers = [
  http.get('/v1/projects/:projectId/report-settings', ({ params }) => {
    if (params.projectId === '00000000-0000-0000-0000-000000000001') {
      return HttpResponse.json(mockReportSettings);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.post('/v1/projects/:projectId/report-settings/template', ({ params }) => {
    if (params.projectId === '00000000-0000-0000-0000-000000000001') {
      return HttpResponse.json(mockUploadResponse);
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.delete(
    '/v1/projects/:projectId/report-settings/template',
    ({ params }) => {
      if (params.projectId === '00000000-0000-0000-0000-000000000001') {
        return new HttpResponse(null, { status: 204 });
      }
      return new HttpResponse(null, { status: 404 });
    },
  ),

  http.patch('/v1/projects/:projectId/report-settings', ({ params }) => {
    if (params.projectId === '00000000-0000-0000-0000-000000000001') {
      return HttpResponse.json(mockReportSettings);
    }
    return new HttpResponse(null, { status: 404 });
  }),
];
