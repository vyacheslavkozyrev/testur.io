import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import RunDetailPanel from './RunDetailPanel';
import type { RunDetailResponse } from '@/types/history.types';

// ─── Mock hooks ───────────────────────────────────────────────────────────────

let mockRunDetailResult: {
  data: RunDetailResponse | undefined;
  isPending: boolean;
} = { data: undefined, isPending: false };

jest.mock('@/hooks/useProjectHistory', () => ({
  useRunDetail: () => mockRunDetailResult,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      history: {
        'panel.title': '{{storyTitle}}',
        'panel.noData': 'Run detail',
        'panel.projectSettingsTooltip': 'Go to project settings',
        'panel.projectSettingsAriaLabel': 'Project settings',
        'panel.closeAriaLabel': 'Close panel',
        'panel.structuredView': 'Structured',
        'panel.rawReport': 'Raw report',
        'panel.rawReportDisabledTooltip': 'No raw report available for this run.',
        'verdict.passed': 'Passed',
        'verdict.failed': 'Failed',
        'recommendation.approve': 'Approve and merge',
        'recommendation.request_fixes': 'Request fixes',
        'recommendation.flag_for_manual_review': 'Flag for manual review',
        'scenarioCard.screenshotAlt': 'Test screenshot',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

const sampleRunDetail: RunDetailResponse = {
  id: 'result-1',
  runId: 'run-1',
  storyTitle: 'User can log in',
  verdict: 'PASSED',
  recommendation: 'approve',
  totalDurationMs: 7000,
  createdAt: '2026-05-16T10:05:00Z',
  scenarioResults: [
    { scenarioId: 'sc-1', title: 'POST /auth — 200', passed: true, durationMs: 400, errorSummary: null, testType: 'api', screenshotUris: [] },
    { scenarioId: 'sc-2', title: 'POST /auth — 401', passed: true, durationMs: 320, errorSummary: null, testType: 'api', screenshotUris: [] },
  ],
  rawCommentMarkdown: '## Report\n**Verdict:** PASSED',
};

function renderComponent(
  projectId = 'project-1',
  runId: string | null = 'run-1',
  onClose = jest.fn(),
) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <RunDetailPanel projectId={projectId} runId={runId} onClose={onClose} />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RunDetailPanel', () => {
  beforeEach(() => {
    mockRunDetailResult = { data: sampleRunDetail, isPending: false };
  });

  it('renders loading skeleton when isPending is true', () => {
    mockRunDetailResult = { data: undefined, isPending: true };
    const { container } = renderComponent();
    // MUI Skeleton renders with role="progressbar" or a div with MuiSkeleton class
    const skeletons = container.querySelectorAll('[class*="MuiSkeleton"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders correct number of ScenarioCards in structured view', () => {
    renderComponent();
    expect(screen.getByText('POST /auth — 200')).toBeInTheDocument();
    expect(screen.getByText('POST /auth — 401')).toBeInTheDocument();
  });

  it('clicking Raw report toggle switches to markdown view', async () => {
    const user = userEvent.setup();
    renderComponent();

    const rawBtn = screen.getByText('Raw report');
    await user.click(rawBtn);

    // Raw markdown content is shown in a pre block
    await waitFor(() => {
      expect(screen.getByText(/## Report/)).toBeInTheDocument();
    });
  });

  it('Raw report toggle is disabled when rawCommentMarkdown is null', () => {
    mockRunDetailResult = {
      data: { ...sampleRunDetail, rawCommentMarkdown: null },
      isPending: false,
    };
    renderComponent();

    const rawBtn = screen.getByText('Raw report');
    expect(rawBtn).toBeDisabled();
  });

  it('clicking close button calls onClose', async () => {
    const user = userEvent.setup();
    const onClose = jest.fn();
    renderComponent('project-1', 'run-1', onClose);

    const closeBtn = screen.getByLabelText('Close panel');
    await user.click(closeBtn);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('maps recommendation correctly for all three values', () => {
    const recommendations = [
      { value: 'approve', label: 'Approve and merge' },
      { value: 'request_fixes', label: 'Request fixes' },
      { value: 'flag_for_manual_review', label: 'Flag for manual review' },
    ];

    recommendations.forEach(({ value, label }) => {
      mockRunDetailResult = {
        data: { ...sampleRunDetail, recommendation: value },
        isPending: false,
      };
      const { unmount } = renderComponent();
      expect(screen.getByText(new RegExp(label))).toBeInTheDocument();
      unmount();
    });
  });
});
