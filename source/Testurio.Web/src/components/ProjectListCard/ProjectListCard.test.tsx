import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';

jest.mock('next/navigation', () => ({
  useRouter: jest.fn(),
}));

import { useRouter } from 'next/navigation';
import ProjectListCard from './ProjectListCard';
import type { ProjectDto } from '@/types/project.types';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      projects: {
        'card.editAriaLabel': 'Edit project',
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

const shortStrategy = 'Focus on API contracts and key user flows.';
const longStrategy =
  'This is a very long testing strategy that exceeds one hundred and twenty characters in total length so that truncation is applied to it by the component.';

const mockProject: ProjectDto = {
  projectId: '00000000-0000-0000-0000-000000000001',
  name: 'Demo Project',
  productUrl: 'https://example.com',
  testingStrategy: shortStrategy,
  customPrompt: null,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
};

beforeEach(() => {
  mockPush.mockClear();
  mockUseRouter.mockReturnValue({ push: mockPush });
});

describe('ProjectListCard', () => {
  it('renders the card link pointing to the project history URL', () => {
    render(
      <Wrapper>
        <ProjectListCard project={mockProject} />
      </Wrapper>,
    );
    const links = screen.getAllByRole('link');
    const historyLink = links.find((l) =>
      l.getAttribute('href')?.includes('/history'),
    );
    expect(historyLink).toBeDefined();
    expect(historyLink!.getAttribute('href')).toBe(
      `/projects/${mockProject.projectId}/history`,
    );
  });

  it('renders the edit icon button with the correct aria-label', () => {
    render(
      <Wrapper>
        <ProjectListCard project={mockProject} />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: 'Edit project' })).toBeInTheDocument();
  });

  it('navigates to settings URL when the edit button is clicked without triggering card navigation', () => {
    render(
      <Wrapper>
        <ProjectListCard project={mockProject} />
      </Wrapper>,
    );
    const editBtn = screen.getByRole('button', { name: 'Edit project' });
    fireEvent.click(editBtn);

    expect(mockPush).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith(
      `/projects/${mockProject.projectId}/settings`,
    );
  });

  it('shows the full testing strategy when it is at or below 120 characters', () => {
    render(
      <Wrapper>
        <ProjectListCard project={mockProject} />
      </Wrapper>,
    );
    expect(screen.getByText(shortStrategy)).toBeInTheDocument();
  });

  it('truncates the testing strategy to 120 characters with an ellipsis when it exceeds the limit', () => {
    render(
      <Wrapper>
        <ProjectListCard project={{ ...mockProject, testingStrategy: longStrategy }} />
      </Wrapper>,
    );
    const expected = longStrategy.slice(0, 120).trimEnd() + '\u2026';
    expect(screen.getByText(expected)).toBeInTheDocument();
    expect(screen.queryByText(longStrategy)).not.toBeInTheDocument();
  });

  it('renders the project name and product URL', () => {
    render(
      <Wrapper>
        <ProjectListCard project={mockProject} />
      </Wrapper>,
    );
    expect(screen.getByText(mockProject.name)).toBeInTheDocument();
    expect(screen.getByText(mockProject.productUrl)).toBeInTheDocument();
  });
});
