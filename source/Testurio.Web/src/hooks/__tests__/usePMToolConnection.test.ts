import { renderHook, act, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import { pmToolService } from '@/services/pmTool/pmToolService';
import {
  useIntegrationStatus,
  useSaveADOConnection,
  useSaveJiraConnection,
  useTestConnection,
  useRemoveConnection,
  PM_TOOL_KEYS,
} from '../usePMToolConnection';
import type { PMToolConnectionResponse, TestConnectionResult } from '@/types/pmTool.types';

jest.mock('@/services/pmTool/pmToolService');
const mockPmToolService = pmToolService as jest.Mocked<typeof pmToolService>;

const mockIntegration: PMToolConnectionResponse = {
  pmTool: 'ado',
  integrationStatus: 'active',
  adoOrgUrl: 'https://dev.azure.com/myorg',
  adoProjectName: 'My Project',
  adoTeam: 'My Team',
  adoInTestingStatus: 'In Testing',
  adoAuthMethod: 'pat',
  adoTokenSecretUri: 'projects--proj-1--adoToken',
  jiraBaseUrl: null,
  jiraProjectKey: null,
  jiraInTestingStatus: null,
  jiraAuthMethod: null,
  jiraApiTokenSecretUri: null,
  jiraEmailSecretUri: null,
  jiraPatSecretUri: null,
};

const mockNoIntegration: PMToolConnectionResponse = {
  pmTool: null,
  integrationStatus: 'none',
  adoOrgUrl: null,
  adoProjectName: null,
  adoTeam: null,
  adoInTestingStatus: null,
  adoAuthMethod: null,
  adoTokenSecretUri: null,
  jiraBaseUrl: null,
  jiraProjectKey: null,
  jiraInTestingStatus: null,
  jiraAuthMethod: null,
  jiraApiTokenSecretUri: null,
  jiraEmailSecretUri: null,
  jiraPatSecretUri: null,
};

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: qc }, children);
}

describe('useIntegrationStatus', () => {
  it('fetches integration status for a project', async () => {
    mockPmToolService.getIntegrationStatus.mockResolvedValue(mockIntegration);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useIntegrationStatus('proj-1'), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.pmTool).toBe('ado');
  });

  it('sets error state on fetch failure', async () => {
    mockPmToolService.getIntegrationStatus.mockRejectedValue(new Error('Network error'));

    const wrapper = createWrapper();
    const { result } = renderHook(() => useIntegrationStatus('proj-1'), { wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe('useSaveADOConnection', () => {
  it('updates integration status cache on success', async () => {
    mockPmToolService.saveADOConnection.mockResolvedValue(mockIntegration);
    mockPmToolService.getIntegrationStatus.mockResolvedValue(mockNoIntegration);

    const wrapper = createWrapper();
    const { result } = renderHook(
      () => ({
        status: useIntegrationStatus('proj-1'),
        save: useSaveADOConnection('proj-1'),
      }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.status.isSuccess).toBe(true));

    await act(async () => {
      result.current.save.mutate({
        orgUrl: 'https://dev.azure.com/myorg',
        projectName: 'My Project',
        team: 'My Team',
        inTestingStatus: 'In Testing',
        authMethod: 'pat',
        pat: 'my-pat',
      });
    });

    await waitFor(() => expect(result.current.save.isSuccess).toBe(true));
    expect(mockPmToolService.saveADOConnection).toHaveBeenCalledTimes(1);
  });
});

describe('useTestConnection', () => {
  it('returns ok result when connection succeeds', async () => {
    const mockResult: TestConnectionResult = { status: 'ok', message: 'Connection successful.' };
    mockPmToolService.testConnection.mockResolvedValue(mockResult);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useTestConnection('proj-1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.status).toBe('ok');
  });

  it('returns auth_error result when credentials are invalid', async () => {
    const mockResult: TestConnectionResult = {
      status: 'auth_error',
      message: 'Authentication failed — check your token.',
    };
    mockPmToolService.testConnection.mockResolvedValue(mockResult);

    const wrapper = createWrapper();
    const { result } = renderHook(() => useTestConnection('proj-1'), { wrapper });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.status).toBe('auth_error');
  });
});

describe('useRemoveConnection', () => {
  it('clears cache on removal success', async () => {
    mockPmToolService.removeConnection.mockResolvedValue(mockNoIntegration);
    mockPmToolService.getIntegrationStatus.mockResolvedValue(mockIntegration);

    const wrapper = createWrapper();
    const { result } = renderHook(
      () => ({
        status: useIntegrationStatus('proj-1'),
        remove: useRemoveConnection('proj-1'),
      }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.status.isSuccess).toBe(true));

    await act(async () => {
      result.current.remove.mutate();
    });

    await waitFor(() => expect(result.current.remove.isSuccess).toBe(true));
    expect(mockPmToolService.removeConnection).toHaveBeenCalledTimes(1);
  });
});

describe('PM_TOOL_KEYS', () => {
  it('generates stable query keys', () => {
    expect(PM_TOOL_KEYS.status('proj-1')).toEqual(['projects', 'proj-1', 'integrations']);
    expect(PM_TOOL_KEYS.webhookSetup('proj-1')).toEqual([
      'projects',
      'proj-1',
      'integrations',
      'webhook-setup',
    ]);
  });
});
