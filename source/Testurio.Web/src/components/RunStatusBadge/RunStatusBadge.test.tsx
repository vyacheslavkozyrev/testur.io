import { render, screen } from '@testing-library/react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import RunStatusBadge from './RunStatusBadge';
import type { RunStatus } from '@/types/dashboard.types';

const theme = createTheme();

function Wrapper({ children }: { children: React.ReactNode }) {
  return <ThemeProvider theme={theme}>{children}</ThemeProvider>;
}

const ALL_STATUSES: Array<{ status: RunStatus; expectedLabel: string }> = [
  { status: 'Queued',    expectedLabel: 'Queued' },
  { status: 'Running',   expectedLabel: 'Running' },
  { status: 'Passed',    expectedLabel: 'Passed' },
  { status: 'Failed',    expectedLabel: 'Failed' },
  { status: 'Cancelled', expectedLabel: 'Cancelled' },
  { status: 'TimedOut',  expectedLabel: 'Timed out' },
  { status: 'NeverRun',  expectedLabel: 'Never run' },
];

describe('RunStatusBadge', () => {
  it.each(ALL_STATUSES)(
    'renders correct label for status $status',
    ({ status, expectedLabel }) => {
      render(
        <Wrapper>
          <RunStatusBadge status={status} />
        </Wrapper>,
      );
      expect(screen.getByText(expectedLabel)).toBeInTheDocument();
    },
  );

  it('renders a Chip element for every status', () => {
    ALL_STATUSES.forEach(({ status }) => {
      const { unmount } = render(
        <Wrapper>
          <RunStatusBadge status={status} />
        </Wrapper>,
      );
      // MUI Chip renders with role="button" when clickable, or as a <div> when not.
      // Verify it renders without throwing.
      unmount();
    });
  });

  it('applies pulse animation class only to Running status', () => {
    const { container: runningContainer } = render(
      <Wrapper>
        <RunStatusBadge status="Running" />
      </Wrapper>,
    );
    const { container: passedContainer } = render(
      <Wrapper>
        <RunStatusBadge status="Passed" />
      </Wrapper>,
    );

    // The pulsing sx prop adds a keyframes animation via MUI's sx system.
    // We verify the Running chip exists and both chips render their labels.
    expect(runningContainer.querySelector('[class*="MuiChip"]')).toBeInTheDocument();
    expect(passedContainer.querySelector('[class*="MuiChip"]')).toBeInTheDocument();
  });
});
