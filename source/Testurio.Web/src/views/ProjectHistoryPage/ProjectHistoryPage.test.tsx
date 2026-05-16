import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import ProjectHistoryPage from './ProjectHistoryPage';
import type { ProjectHistoryResponse } from '@/types/history.types';

// ─── Mock child components ────────────────────────────────────────────────────

jest.mock('@/components/TrendChart/TrendChart', () => ({
  __esModule: true,
  default: () => <div data-testid="trend-chart" />,
}));

jest.mock('@/components/RunHistoryTable/RunHistoryTable', () => ({
  __esModule: true,
  default: ({ runs, onRowClick }: { runs: Array<{ runId: string; storyTitle: string }>; onRowClick: (id: string) => void }) => (
    <div data-testid="run-history-table">
      {runs.map((r) => (
        <button key={r.runId} onClick={() => onRowClick(r.runId)}>
          {r.storyTitle}
        </button>
      ))}
    </div>
  ),
}));

jest.mock('@/components/RunDetailPanel/RunDetailPanel', () => ({
  __esModule: true,
  default: ({ runId, onClose }: { runId: string | null; onClose: () => void }) => (
    <div data-testid="run-detail-panel" data-run-id={runId}>
      <button onClick={onClose}>close</button>
    </div>
  ),
}));

// ─── Mock useProjectHistory hook ──────────────────────────────────────────────

const mockRefetch = jest.fn();

let mockHistoryResult: {
  data: ProjectHistoryResponse | undefined;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  refetch: () => void;
} = {
  data: undefined,
  isPending: false,
  isError: false,
  error: null,
  refetch: mockRefetch,
};

jest.mock('@/hooks/useProjectHistory', () => ({
  useProjectHistory: () => mockHistoryResult,
  useRunDetail: () => ({ data: undefined, isPending: false }),
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      history: {
        'page.title': 'Test History',
        'page.projectSettingsButton': 'Project Settings',
        'page.emptyState': 'No test runs yet.',
        'page.errorMessage': 'Failed to load test history.',
        'page.retryButton': 'Retry',
        'page.projectNotFound': 'Project not found.',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

const SAMPLE_HISTORY: ProjectHistoryResponse = {
  runs: [
    {
      id: 'r1',
      runId: 'run-1',
      storyTitle: 'User can log in',
      verdict: 'PASSED',
      recommendation: 'approve',
      totalApiScenarios: 1,
      passedApiScenarios: 1,
      totalUiE2eScenarios: 0,
      passedUiE2eScenarios: 0,
      totalDurationMs: 3000,
      createdAt: '2026-05-16T10:00:00Z',
    },
  ],
  trendPoints: [],
};

function renderComponent(projectId = 'project-1') {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <ProjectHistoryPage projectId={projectId} />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ProjectHistoryPage', () => {
  beforeEach(() => {
    mockHistoryResult = {
      data: undefined,
      isPending: false,
      isError: false,
      error: null,
      refetch: mockRefetch,
    };
    jest.clearAllMocks();
  });

  it('renders page title', () => {
    mockHistoryResult.data = SAMPLE_HISTORY;
    renderComponent();
    expect(screen.getByText('Test History')).toBeInTheDocument();
  });

  it('renders loading skeleton when isPending is true', () => {
    mockHistoryResult = { ...mockHistoryResult, isPending: true };
    const { container } = renderComponent();
    const skeletons = container.querySelectorAll('[class*="MuiSkeleton"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders empty state when runs list is empty', () => {
    mockHistoryResult.data = { runs: [], trendPoints: [] };
    renderComponent();
    expect(screen.getByText('No test runs yet.')).toBeInTheDocument();
  });

  it('renders error state with retry button on API failure', () => {
    mockHistoryResult = { ...mockHistoryResult, isError: true };
    renderComponent();
    expect(screen.getByText('Failed to load test history.')).toBeInTheDocument();
    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('renders project-not-found message on 404', () => {
    mockHistoryResult = {
      ...mockHistoryResult,
      isError: true,
      error: { response: { status: 404 } },
    };
    renderComponent();
    expect(screen.getByText('Project not found.')).toBeInTheDocument();
  });

  it('Project Settings button has correct href', () => {
    mockHistoryResult.data = SAMPLE_HISTORY;
    renderComponent('abc-123');
    const btn = screen.getByText('Project Settings');
    expect(btn.closest('a')).toHaveAttribute('href', '/projects/abc-123/settings');
  });

  it('clicking a row opens RunDetailPanel with correct runId', async () => {
    const user = userEvent.setup();
    mockHistoryResult.data = SAMPLE_HISTORY;
    renderComponent();

    await user.click(screen.getByText('User can log in'));

    const panel = screen.getByTestId('run-detail-panel');
    expect(panel).toHaveAttribute('data-run-id', 'run-1');
  });

  it('closing the panel resets selectedRunId to null', async () => {
    const user = userEvent.setup();
    mockHistoryResult.data = SAMPLE_HISTORY;
    renderComponent();

    // Open panel
    await user.click(screen.getByText('User can log in'));
    const panel = screen.getByTestId('run-detail-panel');
    expect(panel).toHaveAttribute('data-run-id', 'run-1');

    // Close panel
    await user.click(screen.getByText('close'));
    expect(panel).toHaveAttribute('data-run-id', '');
  });
});
