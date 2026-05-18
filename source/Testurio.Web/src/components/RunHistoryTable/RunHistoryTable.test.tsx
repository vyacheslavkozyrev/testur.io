import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import RunHistoryTable from './RunHistoryTable';
import type { RunHistoryItem } from '@/types/history.types';

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      history: {
        'table.ariaLabel': 'Test run history',
        'table.columnStory': 'Story',
        'table.columnStatus': 'Status',
        'table.columnDate': 'Date',
        'table.columnDuration': 'Duration',
        'table.columnScenarios': 'Scenarios',
        'table.columnUiE2e': 'UI E2E',
        'table.scenarioCount': '{{passed}} / {{total}} passed',
        'table.uiE2eCount': '{{passed}} / {{total}}',
        'table.uiE2eNone': '—',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

function makeRun(overrides: Partial<RunHistoryItem> = {}): RunHistoryItem {
  return {
    id: 'result-1',
    runId: 'run-1',
    storyTitle: 'User can log in',
    verdict: 'PASSED',
    recommendation: 'approve',
    totalApiScenarios: 2,
    passedApiScenarios: 2,
    totalUiE2eScenarios: 1,
    passedUiE2eScenarios: 1,
    totalDurationMs: 5400,
    createdAt: '2026-05-16T10:00:00Z',
    ...overrides,
  };
}

function renderComponent(runs: RunHistoryItem[], onRowClick = jest.fn()) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <RunHistoryTable runs={runs} onRowClick={onRowClick} />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RunHistoryTable', () => {
  it('renders one row per run', () => {
    const runs = [
      makeRun({ id: 'r1', runId: 'run-1', storyTitle: 'Story A' }),
      makeRun({ id: 'r2', runId: 'run-2', storyTitle: 'Story B' }),
      makeRun({ id: 'r3', runId: 'run-3', storyTitle: 'Story C' }),
    ];
    renderComponent(runs);

    expect(screen.getByText('Story A')).toBeInTheDocument();
    expect(screen.getByText('Story B')).toBeInTheDocument();
    expect(screen.getByText('Story C')).toBeInTheDocument();
  });

  it('renders RunStatusBadge with correct status for PASSED verdict', () => {
    renderComponent([makeRun({ verdict: 'PASSED' })]);
    expect(screen.getByText('Passed')).toBeInTheDocument();
  });

  it('renders RunStatusBadge with correct status for FAILED verdict', () => {
    renderComponent([makeRun({ verdict: 'FAILED' })]);
    expect(screen.getByText('Failed')).toBeInTheDocument();
  });

  it('calls onRowClick with correct runId when row is clicked', async () => {
    const user = userEvent.setup();
    const onRowClick = jest.fn();
    const run = makeRun({ runId: 'run-42' });
    renderComponent([run], onRowClick);

    const row = screen.getByText('User can log in').closest('tr');
    expect(row).not.toBeNull();
    await user.click(row!);

    expect(onRowClick).toHaveBeenCalledTimes(1);
    expect(onRowClick).toHaveBeenCalledWith('run-42');
  });

  it('renders duration in seconds', () => {
    renderComponent([makeRun({ totalDurationMs: 5400 })]);
    expect(screen.getByText('5.40 s')).toBeInTheDocument();
  });

  it('renders scenario pass count', () => {
    renderComponent([
      makeRun({ totalApiScenarios: 2, passedApiScenarios: 1, totalUiE2eScenarios: 1, passedUiE2eScenarios: 1 }),
    ]);
    // 1 + 1 passed / 2 + 1 total = "2 / 3 passed"
    expect(screen.getByText('2 / 3 passed')).toBeInTheDocument();
  });

  it('renders column headers', () => {
    renderComponent([makeRun()]);
    expect(screen.getByText('Story')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Duration')).toBeInTheDocument();
    expect(screen.getByText('Scenarios')).toBeInTheDocument();
  });

  // ─── UI E2E column visibility ──────────────────────────────────────────────

  it('all runs have totalUiE2eScenarios === 0 — no UI E2E column rendered', () => {
    const runs = [
      makeRun({ totalUiE2eScenarios: 0, passedUiE2eScenarios: 0 }),
      makeRun({ id: 'r2', runId: 'run-2', totalUiE2eScenarios: 0, passedUiE2eScenarios: 0 }),
    ];
    renderComponent(runs);
    expect(screen.queryByText('UI E2E')).not.toBeInTheDocument();
  });

  it('at least one run has totalUiE2eScenarios > 0 — UI E2E column header present', () => {
    const runs = [
      makeRun({ id: 'r1', runId: 'run-1', totalUiE2eScenarios: 2, passedUiE2eScenarios: 2 }),
      makeRun({ id: 'r2', runId: 'run-2', totalUiE2eScenarios: 0, passedUiE2eScenarios: 0 }),
    ];
    renderComponent(runs);
    expect(screen.getByText('UI E2E')).toBeInTheDocument();
  });

  it('run with totalUiE2eScenarios === 0 in mixed list renders dash in UI E2E cell', () => {
    const runs = [
      makeRun({ id: 'r1', runId: 'run-1', storyTitle: 'Story A', totalUiE2eScenarios: 1, passedUiE2eScenarios: 1 }),
      makeRun({ id: 'r2', runId: 'run-2', storyTitle: 'Story B', totalUiE2eScenarios: 0, passedUiE2eScenarios: 0 }),
    ];
    renderComponent(runs);
    // The dash character for the run with no UI E2E scenarios
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it('run with partial pass renders {passed}/{total} in UI E2E cell', () => {
    const runs = [
      makeRun({ id: 'r1', runId: 'run-1', totalUiE2eScenarios: 3, passedUiE2eScenarios: 1 }),
    ];
    renderComponent(runs);
    expect(screen.getByText('1 / 3')).toBeInTheDocument();
  });

  it('run with all UI E2E scenarios passing renders {passed}/{total}', () => {
    const runs = [
      makeRun({ id: 'r1', runId: 'run-1', totalUiE2eScenarios: 2, passedUiE2eScenarios: 2 }),
    ];
    renderComponent(runs);
    expect(screen.getByText('2 / 2')).toBeInTheDocument();
  });
});
