import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';

// Mock usePathname from next/navigation
jest.mock('next/navigation', () => ({
  usePathname: jest.fn(),
}));

import { usePathname } from 'next/navigation';
import AppSidebar from './AppSidebar';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      layout: {
        'sidebar.dashboard': 'Dashboard',
        'sidebar.projects': 'Projects',
        'sidebar.settings': 'Settings',
        'sidebar.signOut': 'Sign Out',
        'sidebar.signOutAriaLabel': 'Sign out',
        'sidebar.signOutTooltip': 'Sign Out',
        'sidebar.collapseAriaLabel': 'Collapse sidebar',
        'sidebar.expandAriaLabel': 'Expand sidebar',
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

const mockUsePathname = usePathname as jest.Mock;

describe('AppSidebar', () => {
  beforeEach(() => {
    localStorage.clear();
    mockUsePathname.mockReturnValue('/dashboard');
  });

  it('renders Dashboard, Projects, and Settings links', () => {
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Projects')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders Sign Out button', () => {
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: /sign out/i })).toBeInTheDocument();
  });

  it('marks Dashboard link as selected when path is /dashboard', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    const dashboardLink = screen.getByRole('link', { name: /dashboard/i });
    expect(dashboardLink).toHaveClass('Mui-selected');
  });

  it('marks Projects link as selected when path starts with /projects', () => {
    mockUsePathname.mockReturnValue('/projects/abc123/history');
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    const projectsLink = screen.getByRole('link', { name: /projects/i });
    expect(projectsLink).toHaveClass('Mui-selected');
  });

  it('does not mark Dashboard active when path is /settings', () => {
    mockUsePathname.mockReturnValue('/settings');
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    const dashboardLink = screen.getByRole('link', { name: /^dashboard$/i });
    expect(dashboardLink).not.toHaveClass('Mui-selected');
  });

  it('hides labels when collapsed and shows them when expanded', () => {
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    // Initially expanded — labels visible
    expect(screen.getByText('Dashboard')).toBeVisible();

    // Click toggle to collapse
    const toggleBtn = screen.getByRole('button', { name: /collapse sidebar/i });
    fireEvent.click(toggleBtn);

    // Labels hidden when collapsed
    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
  });

  it('persists collapsed state to localStorage', () => {
    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    const toggleBtn = screen.getByRole('button', { name: /collapse sidebar/i });
    fireEvent.click(toggleBtn);

    expect(localStorage.getItem('testurio.sidebarCollapsed')).toBe('true');
  });

  it('disables Sign Out button after clicking it', () => {
    // Mock window.location.href assignment
    Object.defineProperty(window, 'location', {
      writable: true,
      value: { ...window.location, href: '' },
    });

    render(
      <Wrapper>
        <AppSidebar />
      </Wrapper>,
    );
    const signOutBtn = screen.getByRole('button', { name: /sign out/i });
    fireEvent.click(signOutBtn);
    expect(signOutBtn).toBeDisabled();
  });
});
