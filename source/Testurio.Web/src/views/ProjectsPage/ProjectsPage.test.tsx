import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { ProjectDto } from '@/types/project.types';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
}));

import { useRouter } from 'next/navigation';

// ─── Mock useProjects hook ────────────────────────────────────────────────────

const mockRefetch = jest.fn();
const mockUseProjectsState = {
  data: undefined as ProjectDto[] | undefined,
  isPending: false,
  isError: false,
  refetch: mockRefetch,
};

jest.mock('@/hooks/useProject', () => ({
  useProjects: () => mockUseProjectsState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      projects: {
        'page.title': 'Projects',
        'page.createButton': 'Create Project',
        'card.editAriaLabel': 'Edit project',
        'emptyState.heading': 'No projects yet',
        'emptyState.description':
          'Create your first project to start running automated tests against your product.',
        'emptyState.ctaLabel': 'Create your first project',
        'error.message': 'Failed to load projects. Please try again.',
        'error.retryButton': 'Retry',
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

const makeProject = (overrides: Partial<ProjectDto> = {}): ProjectDto => ({
  projectId: '00000000-0000-0000-0000-000000000001',
  name: 'Alpha Project',
  productUrl: 'https://alpha.example.com',
  testingStrategy: 'API contracts only.',
  customPrompt: null,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
  ...overrides,
});

beforeEach(() => {
  jest.clearAllMocks();
  mockUseRouter.mockReturnValue({ push: mockPush });
  mockUseProjectsState.data = undefined;
  mockUseProjectsState.isPending = false;
  mockUseProjectsState.isError = false;
});

// Lazy import after mocks are set up
// eslint-disable-next-line @typescript-eslint/no-require-imports
const { default: ProjectsPage } = require('./ProjectsPage') as {
  default: React.ComponentType;
};

describe('ProjectsPage', () => {
  it('shows loading skeletons while the fetch is in progress', () => {
    mockUseProjectsState.isPending = true;
    mockUseProjectsState.data = undefined;

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    // MUI Skeleton renders with role="progressbar" via wave animation aria by default,
    // but rendered as <span>. Check by looking for skeleton elements directly.
    // Use data from the DOM: Skeleton renders as a div with a class containing "Skeleton".
    const skeletons = document.querySelectorAll('.MuiSkeleton-root');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows the empty state when the API returns an empty array', () => {
    mockUseProjectsState.isPending = false;
    mockUseProjectsState.data = [];

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    expect(screen.getByText('No projects yet')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create your first project' })).toBeInTheDocument();
  });

  it('does not show the empty state while loading', () => {
    mockUseProjectsState.isPending = true;
    mockUseProjectsState.data = undefined;

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    expect(screen.queryByText('No projects yet')).not.toBeInTheDocument();
  });

  it('navigates to /projects/new when "Create your first project" is clicked', () => {
    mockUseProjectsState.data = [];

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Create your first project' }));
    expect(mockPush).toHaveBeenCalledWith('/projects/new');
  });

  it('navigates to /projects/new when the header "Create Project" button is clicked', () => {
    mockUseProjectsState.data = [];

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Create Project' }));
    expect(mockPush).toHaveBeenCalledWith('/projects/new');
  });

  it('shows the error state and Retry button when the fetch fails', () => {
    mockUseProjectsState.isError = true;

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    expect(screen.getByText('Failed to load projects. Please try again.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('calls refetch when the Retry button is clicked', async () => {
    mockUseProjectsState.isError = true;

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => {
      expect(mockRefetch).toHaveBeenCalledTimes(1);
    });
  });

  it('renders project cards when projects are returned', () => {
    const project = makeProject();
    mockUseProjectsState.data = [project];

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    expect(screen.getByText(project.name)).toBeInTheDocument();
  });

  it('renders cards sorted by createdAt descending (newest first)', () => {
    const older = makeProject({
      projectId: '00000000-0000-0000-0000-000000000001',
      name: 'Older Project',
      createdAt: '2026-05-01T00:00:00Z',
    });
    const newer = makeProject({
      projectId: '00000000-0000-0000-0000-000000000002',
      name: 'Newer Project',
      createdAt: '2026-05-10T00:00:00Z',
    });
    // Hook already returns sorted data; pass in sorted order to verify render order
    mockUseProjectsState.data = [newer, older];

    render(
      <Wrapper>
        <ProjectsPage />
      </Wrapper>,
    );

    const cards = screen.getAllByText(/Project/);
    const newerIndex = cards.findIndex((el) => el.textContent === 'Newer Project');
    const olderIndex = cards.findIndex((el) => el.textContent === 'Older Project');
    expect(newerIndex).toBeLessThan(olderIndex);
  });
});
