/**
 * Feature 0024 — Automatic Work Item Status Transition
 * Tests that RunDetailPanel renders status transition outcome rows correctly.
 */
import '@/i18n';
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import RunDetailPanel from './RunDetailPanel';
import type { RunDetailResponse } from '@/types/history.types';

// Mock the hook so we don't need an HTTP server or QueryClientProvider.
jest.mock('@/hooks/useProjectHistory', () => ({
  useRunDetail: jest.fn(),
}));

import { useRunDetail } from '@/hooks/useProjectHistory';

const mockUseRunDetail = useRunDetail as jest.MockedFunction<typeof useRunDetail>;

const baseRun: RunDetailResponse = {
  id: 'result-1',
  runId: 'run-1',
  storyTitle: 'User can log in',
  verdict: 'PASSED',
  recommendation: 'approve',
  totalDurationMs: 5000,
  createdAt: '2026-05-18T10:00:00Z',
  scenarioResults: [],
  rawCommentMarkdown: null,
  statusTransitionOutcome: null,
  statusTransitionError: null,
  statusTransitionedTo: null,
};

function renderPanel(runId: string | null, run: RunDetailResponse | null = null) {
  mockUseRunDetail.mockReturnValue({
    data: run ?? undefined,
    isPending: false,
  } as ReturnType<typeof useRunDetail>);

  return render(
    <RunDetailPanel projectId="proj-1" runId={runId} onClose={jest.fn()} />,
  );
}

describe('RunDetailPanel — status transition row', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  it('does not render transition row when outcome is null', async () => {
    renderPanel('run-1', { ...baseRun, statusTransitionOutcome: null });

    await waitFor(() => {
      expect(screen.getByText(/User can log in/i)).toBeInTheDocument();
    });

    expect(screen.queryByText(/Status transition:/i)).not.toBeInTheDocument();
  });

  it('renders succeeded message when outcome is succeeded', async () => {
    renderPanel('run-1', {
      ...baseRun,
      statusTransitionOutcome: 'succeeded',
      statusTransitionedTo: 'Closed',
    });

    await waitFor(() => {
      expect(screen.getByText(/Status transition:/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/Moved to "Closed"/i)).toBeInTheDocument();
  });

  it('renders not-configured message when outcome is notConfigured', async () => {
    renderPanel('run-1', {
      ...baseRun,
      statusTransitionOutcome: 'notConfigured',
    });

    await waitFor(() => {
      expect(screen.getByText(/Status transition:/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/Not configured/i)).toBeInTheDocument();
  });

  it('renders failed message with error detail when outcome is failed', async () => {
    renderPanel('run-1', {
      ...baseRun,
      statusTransitionOutcome: 'failed',
      statusTransitionError: 'HTTP 404: Transition not found',
    });

    await waitFor(() => {
      expect(screen.getByText(/Status transition:/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/Transition failed/i)).toBeInTheDocument();
    expect(screen.getByText(/HTTP 404/i)).toBeInTheDocument();
  });

  it('does not render panel content when runId is null (drawer closed)', () => {
    mockUseRunDetail.mockReturnValue({
      data: undefined,
      isPending: false,
    } as ReturnType<typeof useRunDetail>);

    render(
      <RunDetailPanel projectId="proj-1" runId={null} onClose={jest.fn()} />,
    );

    expect(screen.queryByText(/Status transition:/i)).not.toBeInTheDocument();
  });
});
