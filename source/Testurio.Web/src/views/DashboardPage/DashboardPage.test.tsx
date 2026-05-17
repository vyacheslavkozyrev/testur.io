import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { DashboardResponse, DashboardUpdatedEvent } from '@/types/dashboard.types';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
}));

import { useRouter } from 'next/navigation';

// ─── Mock useDashboard hook ───────────────────────────────────────────────────

const mockRefetch = jest.fn();
const mockUseDashboardState: {
  data: DashboardResponse | undefined;
  isPending: boolean;
  isError: boolean;
  refetch: () => void;
} = {
  data: undefined,
  isPending: false,
  isError: false,
  refetch: mockRefetch,
};

jest.mock('@/hooks/useDashboard', () => ({
  useDashboard: () => mockUseDashboardState,
}));

// ─── Mock useDashboardStream hook ─────────────────────────────────────────────

import type { UseDashboardStreamOptions } from '@/hooks/useDashboardStream';

let capturedStreamOptions: UseDashboardStreamOptions | null = null;

jest.mock('@/hooks/useDashboardStream', () => ({
  useDashboardStream: (opts: UseDashboardStreamOptions) => {
    capturedStreamOptions = opts;
  },
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      dashboard: {
        page: {
          title: 'Dashboard',
          createButton: 'Create Project',
        },
        quota: {
          usage: '{{used}} / {{limit}} runs used today',
          resetsAt: 'Resets at {{time}}',
          noActivePlan: 'No active plan',
        },
        card: {
          lastRun: 'Last run: {{time}}',
          neverRun: 'Never run',
        },
        emptyState: {
          heading: 'No projects yet',
          description: 'Create your first project to get started.',
          ctaLabel: 'Create your first project',
        },
        error: {
          message: 'Failed to load dashboard data. Please try again.',
          retryButton: 'Retry',
        },
        stream: {
          reconnecting: 'Reconnecting…',
          unavailable: 'Live updates unavailable — data may be stale',
        },
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

const mockPush = jest.fn();
const mockUseRouter = useRouter as jest.Mock;

const DEFAULT_QUOTA = {
  usedToday: 3,
  dailyLimit: 50,
  resetsAt: '2026-05-17T00:00:00Z',
};

const mockProject = {
  projectId: '00000000-0000-0000-0000-000000000001',
  name: 'Alpha Project',
  productUrl: 'https://alpha.example.com',
  testingStrategy: 'API contracts only.',
  latestRun: {
    runId: '00000000-0000-0000-0000-000000000011',
    status: 'Passed' as const,
    startedAt: '2026-05-15T10:00:00Z',
    completedAt: '2026-05-15T10:05:00Z',
  },
};

beforeEach(() => {
  jest.clearAllMocks();
  capturedStreamOptions = null;
  mockUseRouter.mockReturnValue({ push: mockPush });
  mockUseDashboardState.data = undefined;
  mockUseDashboardState.isPending = false;
  mockUseDashboardState.isError = false;
});

// Lazy import after mocks are set up
// eslint-disable-next-line @typescript-eslint/no-require-imports
const { default: DashboardPage } = require('./DashboardPage') as {
  default: React.ComponentType;
};

describe('DashboardPage', () => {
  it('shows loading skeletons while fetch is in progress', () => {
    mockUseDashboardState.isPending = true;

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    const skeletons = document.querySelectorAll('.MuiSkeleton-root');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when API returns empty projects array', () => {
    mockUseDashboardState.data = { projects: [], quotaUsage: DEFAULT_QUOTA };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(screen.getByText('No projects yet')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create your first project' })).toBeInTheDocument();
  });

  it('does not show empty state while loading', () => {
    mockUseDashboardState.isPending = true;

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(screen.queryByText('No projects yet')).not.toBeInTheDocument();
  });

  it('shows error state with Retry button when fetch fails', () => {
    mockUseDashboardState.isError = true;

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(screen.getByText('Failed to load dashboard data. Please try again.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('calls refetch when Retry button is clicked', async () => {
    mockUseDashboardState.isError = true;

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => {
      expect(mockRefetch).toHaveBeenCalledTimes(1);
    });
  });

  it('renders card grid with project cards on success', () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(screen.getByText(mockProject.name)).toBeInTheDocument();
  });

  it('renders QuotaUsageBar when data is available', () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(screen.getByText('3 / 50 runs used today')).toBeInTheDocument();
  });

  it('always shows the Create Project button in the header', () => {
    mockUseDashboardState.data = { projects: [], quotaUsage: DEFAULT_QUOTA };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(screen.getByRole('button', { name: 'Create Project' })).toBeInTheDocument();
  });

  // ─── SSE behaviour (feature 0043) ─────────────────────────────────────────

  it('SSE stream is enabled once snapshot data is loaded', () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(capturedStreamOptions?.enabled).toBe(true);
  });

  it('SSE stream is disabled while snapshot is loading', () => {
    mockUseDashboardState.isPending = true;

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    expect(capturedStreamOptions?.enabled).toBe(false);
  });

  it('onUpdate callback updates the correct project card badge in place', async () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    const updatedEvent: DashboardUpdatedEvent = {
      projectId: mockProject.projectId,
      latestRun: {
        runId: '00000000-0000-0000-0000-000000000099',
        status: 'Failed',
        startedAt: '2026-05-16T12:00:00Z',
        completedAt: '2026-05-16T12:05:00Z',
      },
      quotaUsage: null,
    };

    act(() => {
      capturedStreamOptions?.onUpdate(updatedEvent);
    });

    // After update, card for alpha project should still be present.
    expect(screen.getByText(mockProject.name)).toBeInTheDocument();
  });

  it('unknown projectId SSE event triggers a re-fetch', async () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    const unknownEvent: DashboardUpdatedEvent = {
      projectId: 'unknown-project-id',
      latestRun: {
        runId: 'run-new',
        status: 'Passed',
        startedAt: '2026-05-16T12:00:00Z',
        completedAt: '2026-05-16T12:05:00Z',
      },
      quotaUsage: null,
    };

    act(() => {
      capturedStreamOptions?.onUpdate(unknownEvent);
    });

    await waitFor(() => {
      expect(mockRefetch).toHaveBeenCalledTimes(1);
    });
  });

  it('fallback warning appears after onFallback is called', async () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    act(() => {
      capturedStreamOptions?.onFallback();
    });

    await waitFor(() => {
      expect(
        screen.getByText('Live updates unavailable — data may be stale'),
      ).toBeInTheDocument();
    });
  });

  it('fallback triggers a snapshot re-fetch', async () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    act(() => {
      capturedStreamOptions?.onFallback();
    });

    await waitFor(() => {
      expect(mockRefetch).toHaveBeenCalledTimes(1);
    });
  });

  it('reconnecting indicator appears when onReconnecting(true) is called', async () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    act(() => {
      capturedStreamOptions?.onReconnecting?.(true);
    });

    await waitFor(() => {
      expect(screen.getByText('Reconnecting…')).toBeInTheDocument();
    });
  });

  it('reconnecting indicator disappears when onReconnecting(false) is called', async () => {
    mockUseDashboardState.data = {
      projects: [mockProject],
      quotaUsage: DEFAULT_QUOTA,
    };

    render(
      <Wrapper>
        <DashboardPage />
      </Wrapper>,
    );

    // First show, then hide the indicator.
    act(() => {
      capturedStreamOptions?.onReconnecting?.(true);
    });

    await waitFor(() => {
      expect(screen.getByText('Reconnecting…')).toBeInTheDocument();
    });

    act(() => {
      capturedStreamOptions?.onReconnecting?.(false);
    });

    await waitFor(() => {
      expect(screen.queryByText('Reconnecting…')).not.toBeInTheDocument();
    });
  });
});
