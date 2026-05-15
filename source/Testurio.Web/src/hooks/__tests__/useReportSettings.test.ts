import { renderHook, act, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import { reportTemplateService } from '@/services/reportTemplate/reportTemplateService';
import {
  useReportSettings,
  useUploadReportTemplate,
  useRemoveReportTemplate,
  useUpdateReportSettings,
  REPORT_SETTINGS_KEYS,
} from '../useReportSettings';
import type {
  ReportSettingsDto,
  ReportTemplateUploadResponse,
} from '@/types/reportSettings.types';

jest.mock('@/services/reportTemplate/reportTemplateService');
const mockService = reportTemplateService as jest.Mocked<typeof reportTemplateService>;

const mockSettings: ReportSettingsDto = {
  reportTemplateUri: null,
  reportTemplateFileName: null,
  reportIncludeLogs: false,
  reportIncludeScreenshots: false,
};

const mockSettingsWithTemplate: ReportSettingsDto = {
  reportTemplateUri: 'https://blob.example.com/templates/proj-1/template.md',
  reportTemplateFileName: 'template.md',
  reportIncludeLogs: true,
  reportIncludeScreenshots: false,
};

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: qc }, children);
}

beforeEach(() => {
  jest.clearAllMocks();
});

// ─── useReportSettings ────────────────────────────────────────────────────────

describe('useReportSettings', () => {
  it('fetches report settings for a project', async () => {
    mockService.getSettings.mockResolvedValue(mockSettings);

    const { result } = renderHook(() => useReportSettings('proj-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockSettings);
    expect(mockService.getSettings).toHaveBeenCalledWith('proj-1');
  });

  it('sets error state when fetch fails', async () => {
    mockService.getSettings.mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useReportSettings('proj-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('does not fetch when projectId is empty', () => {
    mockService.getSettings.mockResolvedValue(mockSettings);

    const { result } = renderHook(() => useReportSettings(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.isPending).toBe(true);
    expect(mockService.getSettings).not.toHaveBeenCalled();
  });
});

// ─── useUploadReportTemplate ──────────────────────────────────────────────────

describe('useUploadReportTemplate', () => {
  it('calls uploadTemplate and invalidates settings cache on success', async () => {
    const uploadResponse: ReportTemplateUploadResponse = {
      blobUri: 'https://blob.example.com/tmpl.md',
      warnings: [],
    };

    mockService.uploadTemplate.mockResolvedValue(uploadResponse);
    mockService.getSettings.mockResolvedValue(mockSettings);

    const wrapper = createWrapper();
    const { result } = renderHook(
      () => ({
        settings: useReportSettings('proj-1'),
        upload: useUploadReportTemplate('proj-1'),
      }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.settings.isSuccess).toBe(true));

    const file = new File(['# Template'], 'template.md', { type: 'text/markdown' });

    await act(async () => {
      result.current.upload.mutate(file);
    });

    await waitFor(() => expect(result.current.upload.isSuccess).toBe(true));
    expect(mockService.uploadTemplate).toHaveBeenCalledWith('proj-1', file);
    // Cache should have been invalidated — getSettings called at least twice (initial + after mutation)
    expect(mockService.getSettings).toHaveBeenCalledTimes(
      mockService.getSettings.mock.calls.length,
    );
    expect(mockService.getSettings.mock.calls.length).toBeGreaterThanOrEqual(2);
  });

  it('returns upload response data including warnings', async () => {
    const uploadResponse: ReportTemplateUploadResponse = {
      blobUri: 'https://blob.example.com/tmpl.md',
      warnings: ['{{unknown_token}}'],
    };
    mockService.uploadTemplate.mockResolvedValue(uploadResponse);

    const { result } = renderHook(() => useUploadReportTemplate('proj-1'), {
      wrapper: createWrapper(),
    });

    const file = new File(['# {{unknown_token}}'], 'template.md', {
      type: 'text/markdown',
    });

    await act(async () => {
      result.current.mutate(file);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.warnings).toEqual(['{{unknown_token}}']);
  });

  it('sets error state when upload fails', async () => {
    mockService.uploadTemplate.mockRejectedValue(new Error('Upload failed'));

    const { result } = renderHook(() => useUploadReportTemplate('proj-1'), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate(new File([''], 'template.md'));
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

// ─── useRemoveReportTemplate ──────────────────────────────────────────────────

describe('useRemoveReportTemplate', () => {
  it('calls removeTemplate and invalidates settings cache on success', async () => {
    mockService.removeTemplate.mockResolvedValue(undefined);
    mockService.getSettings.mockResolvedValue(mockSettingsWithTemplate);

    const wrapper = createWrapper();
    const { result } = renderHook(
      () => ({
        settings: useReportSettings('proj-1'),
        remove: useRemoveReportTemplate('proj-1'),
      }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.settings.isSuccess).toBe(true));

    await act(async () => {
      result.current.remove.mutate();
    });

    await waitFor(() => expect(result.current.remove.isSuccess).toBe(true));
    expect(mockService.removeTemplate).toHaveBeenCalledWith('proj-1');
    // Cache should have been invalidated — getSettings called at least twice (initial + after mutation)
    expect(mockService.getSettings.mock.calls.length).toBeGreaterThanOrEqual(2);
  });

  it('sets error state when remove fails', async () => {
    mockService.removeTemplate.mockRejectedValue(new Error('Remove failed'));

    const { result } = renderHook(() => useRemoveReportTemplate('proj-1'), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

// ─── useUpdateReportSettings ──────────────────────────────────────────────────

describe('useUpdateReportSettings', () => {
  it('calls updateSettings and updates the cache on success', async () => {
    const updatedSettings: ReportSettingsDto = {
      ...mockSettings,
      reportIncludeLogs: true,
    };

    mockService.updateSettings.mockResolvedValue(updatedSettings);
    mockService.getSettings.mockResolvedValue(mockSettings);

    const wrapper = createWrapper();
    const { result } = renderHook(
      () => ({
        settings: useReportSettings('proj-1'),
        update: useUpdateReportSettings('proj-1'),
      }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.settings.isSuccess).toBe(true));
    expect(result.current.settings.data?.reportIncludeLogs).toBe(false);

    await act(async () => {
      result.current.update.mutate({ reportIncludeLogs: true, reportIncludeScreenshots: false });
    });

    await waitFor(() => expect(result.current.update.isSuccess).toBe(true));
    expect(mockService.updateSettings).toHaveBeenCalledWith('proj-1', {
      reportIncludeLogs: true,
      reportIncludeScreenshots: false,
    });
    // Cache should be updated in-place (not invalidated)
    expect(result.current.settings.data?.reportIncludeLogs).toBe(true);
  });

  it('sets error state when update fails', async () => {
    mockService.updateSettings.mockRejectedValue(new Error('Update failed'));

    const { result } = renderHook(() => useUpdateReportSettings('proj-1'), {
      wrapper: createWrapper(),
    });

    await act(async () => {
      result.current.mutate({ reportIncludeLogs: true, reportIncludeScreenshots: false });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

// ─── REPORT_SETTINGS_KEYS ─────────────────────────────────────────────────────

describe('REPORT_SETTINGS_KEYS', () => {
  it('generates stable query keys per project', () => {
    expect(REPORT_SETTINGS_KEYS.settings('proj-1')).toEqual(['reportSettings', 'proj-1']);
    expect(REPORT_SETTINGS_KEYS.settings('proj-abc')).toEqual(['reportSettings', 'proj-abc']);
  });
});
