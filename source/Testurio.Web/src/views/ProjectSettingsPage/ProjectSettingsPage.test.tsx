import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { ProjectDto } from '@/types/project.types';
import type { ReportSettingsDto } from '@/types/reportSettings.types';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  useParams: jest.fn(),
  useRouter: jest.fn(),
}));

import { useParams, useRouter } from 'next/navigation';

// ─── Mock hooks ───────────────────────────────────────────────────────────────

const mockUpdateProject = {
  mutate: jest.fn(),
  mutateAsync: jest.fn().mockResolvedValue({}),
  isPending: false,
  isError: false,
  isSuccess: false,
  reset: jest.fn(),
};

const mockDeleteProject = {
  mutate: jest.fn(),
  isPending: false,
};

const mockUpdateReportSettings = {
  mutateAsync: jest.fn().mockResolvedValue({}),
  isPending: false,
};

const mockUseProjectState = {
  data: undefined as ProjectDto | undefined,
  isPending: false,
  isError: false,
};

const mockUseReportSettingsState = {
  data: undefined as ReportSettingsDto | undefined,
  isPending: false,
  isError: false,
};

jest.mock('@/hooks/useProject', () => ({
  useProject: () => mockUseProjectState,
  useUpdateProject: () => mockUpdateProject,
  useDeleteProject: () => mockDeleteProject,
  usePromptCheck: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
  useUpdateWorkItemTypeFilter: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
}));

jest.mock('@/hooks/useReportSettings', () => ({
  useReportSettings: () => mockUseReportSettingsState,
  useUpdateReportSettings: () => mockUpdateReportSettings,
  useUploadReportTemplate: () => ({ mutate: jest.fn(), isPending: false }),
  useRemoveReportTemplate: () => ({ mutate: jest.fn(), isPending: false }),
}));

jest.mock('@/hooks/useProjectAccess', () => ({
  useProjectAccess: () => ({
    data: {
      projectId: '00000000-0000-0000-0000-000000000001',
      accessMode: 'ipAllowlist',
      basicAuthUser: null,
      headerTokenName: null,
    },
    isPending: false,
    isError: false,
  }),
  useUpdateProjectAccess: () => ({
    mutate: jest.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
  }),
}));

jest.mock('@/hooks/usePMToolConnection', () => ({
  useIntegrationStatus: () => ({ data: undefined, isPending: true, isError: false }),
  useSaveADOConnection: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
  useSaveJiraConnection: () => ({ mutate: jest.fn(), isPending: false, isError: false }),
  useTestConnection: () => ({ mutate: jest.fn(), isPending: false, data: undefined }),
  useRemoveConnection: () => ({ mutate: jest.fn(), isPending: false }),
  useWebhookSetup: () => ({ data: undefined, isPending: false }),
  useRegenerateWebhookSecret: () => ({ mutate: jest.fn(), isPending: false }),
  useUpdateToken: () => ({ mutate: jest.fn(), isPending: false }),
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      project: {
        'settings.title': '{{name}}',
        'settings.breadcrumbProjects': 'Projects',
        'settings.breadcrumbSettings': 'Settings',
        'settings.loadError': 'Failed to load project. Please try again.',
        'settings.saveError': 'Failed to save changes. Please try again.',
        'settings.saveSuccess': 'Changes saved successfully.',
        'settings.dangerZone.title': 'Danger Zone',
        'settings.dangerZone.description': 'Permanently delete this project.',
        'settings.dangerZone.deleteButton': 'Delete Project',
        'tabs.settings': 'Settings',
        'tabs.integration': 'Integration',
        'saveBar.save': 'Save Changes',
        'saveBar.noChanges': 'No changes',
        'saveBar.saving': 'Saving…',
        'saveBar.saved': 'Saved ✓',
        'unsavedBanner.message': 'You have unsaved changes in Settings.',
        'unsavedBanner.link': 'Go to Settings',
        'customPrompt.section.title': 'Custom Test Generation Prompt',
        'customPrompt.section.description': 'Add optional instructions.',
        'customPrompt.field.label': 'Custom Prompt',
        'customPrompt.field.placeholder': '',
        'customPrompt.field.overLimit': 'Too long.',
        'customPrompt.field.conflictWarning': 'May conflict.',
        'customPrompt.check.button': 'Check Prompt',
        'customPrompt.check.error': 'Check failed.',
        'customPrompt.check.resultsTitle': 'AI Feedback',
        'customPrompt.check.clarity': 'Clarity',
        'customPrompt.check.specificity': 'Specificity',
        'customPrompt.check.potentialConflicts': 'Conflicts',
        'customPrompt.preview.title': 'Preview',
        'customPrompt.preview.readOnly': 'Read-only',
        'customPrompt.preview.basePrompt': '[Base]',
        'customPrompt.preview.strategyLabel': '[Strategy]',
        'customPrompt.preview.customLabel': '[Custom]',
        'customPrompt.preview.noCustomPrompt': '[None]',
        'form.titleEdit': 'Edit Project',
        'form.titleCreate': 'Create Project',
        'form.fields.name': 'Project Name',
        'form.fields.productUrl': 'Product URL',
        'form.fields.testingStrategy': 'Testing Strategy',
        'form.validation.nameRequired': 'Project name is required.',
        'form.validation.nameMaxLength': 'Too long.',
        'form.validation.productUrlRequired': 'URL required.',
        'form.validation.productUrlInvalid': 'Invalid URL.',
        'form.validation.testingStrategyRequired': 'Strategy required.',
        'form.validation.testingStrategyMaxLength': 'Too long.',
        'form.actions.create': 'Create Project',
        'form.actions.cancel': 'Cancel',
        'deleteDialog.title': 'Delete Project',
        'deleteDialog.message': 'Delete "{{name}}"?',
        'deleteDialog.actions.confirm': 'Delete',
        'deleteDialog.actions.cancel': 'Cancel',
      },
      reportSettings: {
        sectionTitle: 'Report Format & Attachment Settings',
        saveButton: 'Save',
        saveError: 'Failed to save.',
        saveSuccess: 'Saved.',
        loadError: 'Failed to load.',
        'attachments.includeLogs': 'Include Logs',
        'attachments.includeScreenshots': 'Include Screenshots',
        'template.label': 'Report Template',
        'template.upload': 'Upload',
        'template.remove': 'Remove',
        'template.none': 'No template',
        'template.uploadError': 'Upload failed.',
        'template.removeError': 'Remove failed.',
      },
      pmTool: {
        'page.loadError': 'Failed to load integration.',
        'page.title': 'PM Tool Integration',
        'page.description': 'Connect your PM tool.',
        'page.selectTool': 'Select a tool',
        'page.connectAdo': 'Connect ADO',
        'page.connectJira': 'Connect Jira',
        'page.saveError': 'Save failed.',
        'page.dangerZone': 'Danger Zone',
        'remove.button': 'Remove Integration',
        'status.connected': 'Connected',
        'status.notConnected': 'Not connected',
      },
    },
  },
});

const theme = createTheme();

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>{children}</I18nextProvider>
    </ThemeProvider>
  );
}

const PROJECT_ID = '00000000-0000-0000-0000-000000000001';

const mockProject: ProjectDto = {
  projectId: PROJECT_ID,
  name: 'Demo Project',
  productUrl: 'https://example.com',
  testingStrategy: 'API contracts.',
  customPrompt: null,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
};

const mockReportSettings: ReportSettingsDto = {
  reportTemplateUri: null,
  reportTemplateFileName: null,
  reportIncludeLogs: true,
  reportIncludeScreenshots: true,
};

const mockPush = jest.fn();
const mockUseRouter = useRouter as jest.Mock;
const mockUseParams = useParams as jest.Mock;

beforeEach(() => {
  jest.clearAllMocks();
  mockUseRouter.mockReturnValue({ push: mockPush });
  mockUseParams.mockReturnValue({ projectId: PROJECT_ID });

  mockUseProjectState.data = mockProject;
  mockUseProjectState.isPending = false;
  mockUseProjectState.isError = false;

  mockUseReportSettingsState.data = mockReportSettings;
  mockUseReportSettingsState.isPending = false;
  mockUseReportSettingsState.isError = false;

  mockUpdateProject.mutateAsync.mockResolvedValue(mockProject);
  mockUpdateReportSettings.mutateAsync.mockResolvedValue(mockReportSettings);
});

// Lazy import after mocks are set up
// eslint-disable-next-line @typescript-eslint/no-require-imports
const { default: ProjectSettingsPage } = require('./ProjectSettingsPage') as {
  default: React.ComponentType;
};

describe('ProjectSettingsPage', () => {
  it('renders the project name as page title and breadcrumbs', () => {
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    expect(screen.getByRole('heading', { name: 'Demo Project' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Projects' })).toBeInTheDocument();
    expect(screen.getByText('Settings', { selector: 'p' })).toBeInTheDocument();
  });

  it('renders Settings tab active by default', () => {
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    expect(screen.getByRole('tab', { name: 'Settings' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Integration' })).toHaveAttribute('aria-selected', 'false');
  });

  it('shows the form fields and custom prompt section on Settings tab', () => {
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    expect(screen.getByLabelText(/project name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/custom prompt/i)).toBeInTheDocument();
  });

  it('switches to Integration tab when clicked', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    await user.click(screen.getByRole('tab', { name: 'Integration' }));
    expect(screen.getByRole('tab', { name: 'Integration' })).toHaveAttribute('aria-selected', 'true');
    // Settings form fields should not be visible
    expect(screen.queryByLabelText(/project name/i)).not.toBeInTheDocument();
  });

  it('switches back to Settings tab when Settings tab is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    await user.click(screen.getByRole('tab', { name: 'Integration' }));
    await user.click(screen.getByRole('tab', { name: 'Settings' }));
    expect(screen.getByLabelText(/project name/i)).toBeInTheDocument();
  });

  it('shows "No changes" save button initially (clean state)', () => {
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    const saveButton = screen.getByRole('button', { name: /no changes/i });
    expect(saveButton).toBeDisabled();
  });

  it('shows error alert when project fails to load', () => {
    mockUseProjectState.data = undefined;
    mockUseProjectState.isError = true;

    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    expect(screen.getByText('Failed to load project. Please try again.')).toBeInTheDocument();
  });

  it('shows loading spinner while project is pending', () => {
    mockUseProjectState.data = undefined;
    mockUseProjectState.isPending = true;

    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );
    expect(document.querySelector('.MuiCircularProgress-root')).toBeInTheDocument();
  });

  it('shows unsaved-changes banner when on Integration tab with dirty custom prompt', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );

    // Type in custom prompt to dirty the state
    const promptField = screen.getByLabelText(/custom prompt/i);
    await user.type(promptField, 'Some new instructions');

    // Switch to Integration tab
    await user.click(screen.getByRole('tab', { name: 'Integration' }));

    await waitFor(() => {
      expect(screen.getByText('You have unsaved changes in Settings.')).toBeInTheDocument();
    });
  });

  it('navigates back to Settings when clicking "Go to Settings" in the banner', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );

    const promptField = screen.getByLabelText(/custom prompt/i);
    await user.type(promptField, 'Some instructions');
    await user.click(screen.getByRole('tab', { name: 'Integration' }));

    await waitFor(() => {
      expect(screen.getByText('You have unsaved changes in Settings.')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Go to Settings' }));
    expect(screen.getByRole('tab', { name: 'Settings' })).toHaveAttribute('aria-selected', 'true');
  });

  it('calls save mutations on unified save and shows Saved state on success', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );

    // Dirty the custom prompt
    const promptField = screen.getByLabelText(/custom prompt/i);
    await user.type(promptField, 'Extra instructions');

    // Save bar should become enabled
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save changes/i })).not.toBeDisabled();
    });

    await act(async () => {
      await user.click(screen.getByRole('button', { name: /save changes/i }));
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /saved/i })).toBeInTheDocument();
    });
  });

  it('shows per-section error alert when report settings save fails', async () => {
    const user = userEvent.setup();
    mockUpdateReportSettings.mutateAsync.mockRejectedValue(new Error('Network error'));

    render(
      <Wrapper>
        <ProjectSettingsPage />
      </Wrapper>,
    );

    // Dirty the custom prompt so save becomes available
    const promptField = screen.getByLabelText(/custom prompt/i);
    await user.type(promptField, 'Extra instructions');

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save changes/i })).not.toBeDisabled();
    });

    await act(async () => {
      await user.click(screen.getByRole('button', { name: /save changes/i }));
    });

    await waitFor(() => {
      expect(screen.getAllByText('Failed to save changes. Please try again.').length).toBeGreaterThan(0);
    });

    // Save button should return to enabled for retry
    expect(screen.getByRole('button', { name: /save changes/i })).not.toBeDisabled();
  });
});
