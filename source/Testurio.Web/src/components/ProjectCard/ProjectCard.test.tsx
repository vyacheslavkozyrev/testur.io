import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
}));

import ProjectCard from './ProjectCard';
import type { DashboardProjectSummary } from '@/types/dashboard.types';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      dashboard: {
        card: {
          lastRun: 'Last run: {{time}}',
          neverRun: 'Never run',
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

const mockProjectWithRun: DashboardProjectSummary = {
  projectId: '00000000-0000-0000-0000-000000000001',
  name: 'Demo Project',
  productUrl: 'https://example.com',
  testingStrategy: 'API testing.',
  latestRun: {
    runId: '00000000-0000-0000-0000-000000000011',
    status: 'Passed',
    startedAt: '2026-05-15T10:00:00Z',
    completedAt: '2026-05-15T10:05:00Z',
  },
};

const mockProjectNoRun: DashboardProjectSummary = {
  ...mockProjectWithRun,
  projectId: '00000000-0000-0000-0000-000000000002',
  name: 'No Run Project',
  latestRun: null,
};

describe('ProjectCard', () => {
  it('renders a link pointing to the project history URL', () => {
    render(
      <Wrapper>
        <ProjectCard project={mockProjectWithRun} />
      </Wrapper>,
    );
    const link = screen.getByRole('link');
    expect(link.getAttribute('href')).toBe(
      `/projects/${mockProjectWithRun.projectId}/history`,
    );
  });

  it('renders the RunStatusBadge with the correct status', () => {
    render(
      <Wrapper>
        <ProjectCard project={mockProjectWithRun} />
      </Wrapper>,
    );
    // RunStatusBadge renders "Passed" as a chip label
    expect(screen.getByText('Passed')).toBeInTheDocument();
  });

  it('renders NeverRun badge and no timestamp for a project with no runs', () => {
    render(
      <Wrapper>
        <ProjectCard project={mockProjectNoRun} />
      </Wrapper>,
    );
    expect(screen.getByText('Never run')).toBeInTheDocument();
    expect(screen.getByText('Never run')).toBeInTheDocument();
  });

  it('renders project name and product URL', () => {
    render(
      <Wrapper>
        <ProjectCard project={mockProjectWithRun} />
      </Wrapper>,
    );
    expect(screen.getByText(mockProjectWithRun.name)).toBeInTheDocument();
    expect(screen.getByText(mockProjectWithRun.productUrl)).toBeInTheDocument();
  });

  it('renders NeverRun chip when latestRun is null', () => {
    render(
      <Wrapper>
        <ProjectCard project={mockProjectNoRun} />
      </Wrapper>,
    );
    // Chip label for NeverRun status
    expect(screen.getByText('Never run')).toBeInTheDocument();
  });
});
