import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import IntegrationPage from './IntegrationPage';
import { useIntegrationStatus } from '@/hooks/usePMToolConnection';
import { useProject } from '@/hooks/useProject';
import type { PMToolConnectionResponse } from '@/types/pmTool.types';
import type { ProjectDto } from '@/types/project.types';

jest.mock('next/navigation', () => ({
  useParams: () => ({ projectId: 'proj-001' }),
  useRouter: () => ({ push: jest.fn(), replace: jest.fn() }),
}));

jest.mock('@/hooks/usePMToolConnection', () => ({
  useIntegrationStatus: jest.fn(),
  useSaveADOConnection: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
  useSaveJiraConnection: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
  useTestConnection: () => ({ mutate: jest.fn(), isPending: false, isError: false, data: undefined }),
  useRemoveConnection: () => ({ mutate: jest.fn(), isPending: false }),
  useWebhookSetup: () => ({ data: null, isPending: false }),
  useRegenerateWebhookSecret: () => ({ mutate: jest.fn(), isPending: false }),
  useUpdateToken: () => ({ mutate: jest.fn(), isPending: false }),
}));

jest.mock('@/hooks/useProject', () => ({
  useProject: jest.fn(),
  useUpdateWorkItemTypeFilter: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
}));

const mockUseIntegrationStatus = useIntegrationStatus as jest.Mock;
const mockUseProject = useProject as jest.Mock;

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      pmTool: {
        'workItemTypeFilter.title': 'Work Item Type Filter',
      },
    },
  },
});

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>{children}</I18nextProvider>
    </ThemeProvider>
  );
}

const mockProject: ProjectDto = {
  projectId: 'proj-001',
  name: 'Test Project',
  productUrl: 'https://example.com',
  testingStrategy: 'smoke',
  customPrompt: null,
  allowedWorkItemTypes: ['Story', 'Bug'],
  createdAt: '2026-05-15T00:00:00Z',
  updatedAt: '2026-05-15T00:00:00Z',
};

const configuredIntegration: PMToolConnectionResponse = {
  pmTool: 'jira',
  integrationStatus: 'active',
  adoOrgUrl: null,
  adoProjectName: null,
  adoTeam: null,
  adoInTestingStatus: null,
  adoAuthMethod: null,
  adoTokenSecretUri: null,
  jiraBaseUrl: 'https://my-org.atlassian.net',
  jiraProjectKey: 'PROJ',
  jiraInTestingStatus: 'In Testing',
  jiraAuthMethod: 'apiToken',
  jiraApiTokenSecretUri: 'secret-ref',
  jiraEmailSecretUri: null,
  jiraPatSecretUri: null,
};

const noIntegration: PMToolConnectionResponse = {
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

describe('IntegrationPage — WorkItemTypeFilter visibility', () => {
  beforeEach(() => {
    mockUseProject.mockReturnValue({ data: mockProject, isPending: false });
  });

  it('shows the Work Item Type Filter section when a PM tool is configured (AC-001)', () => {
    mockUseIntegrationStatus.mockReturnValue({
      data: configuredIntegration,
      isPending: false,
      isError: false,
    });

    render(
      <Wrapper>
        <IntegrationPage />
      </Wrapper>,
    );

    expect(screen.getByText('Work Item Type Filter')).toBeInTheDocument();
  });

  it('hides the Work Item Type Filter section when no PM tool is configured (AC-011)', () => {
    mockUseIntegrationStatus.mockReturnValue({
      data: noIntegration,
      isPending: false,
      isError: false,
    });

    render(
      <Wrapper>
        <IntegrationPage />
      </Wrapper>,
    );

    expect(screen.queryByText('Work Item Type Filter')).not.toBeInTheDocument();
  });
});
