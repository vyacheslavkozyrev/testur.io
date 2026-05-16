import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';

// Mock child hooks so we don't need a real auth session or browser APIs
jest.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => ({
    id: 'user-1',
    displayName: 'Jane Smith',
    email: 'jane.smith@example.com',
    avatarUrl: undefined,
  }),
}));

jest.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      layout: {
        'header.logoText': 'Testurio',
        'header.logoAriaLabel': 'Testurio — go to dashboard',
        'sidebar.dashboard': 'Dashboard',
        'sidebar.projects': 'Projects',
        'sidebar.settings': 'Settings',
        'sidebar.signOut': 'Sign Out',
        'sidebar.signOutAriaLabel': 'Sign out',
        'sidebar.collapseAriaLabel': 'Collapse sidebar',
        'sidebar.expandAriaLabel': 'Expand sidebar',
      },
    },
  },
});

const theme = createTheme();

import PrivateCabinetLayout from './PrivateCabinetLayout';

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>{children}</I18nextProvider>
    </ThemeProvider>
  );
}

describe('PrivateCabinetLayout', () => {
  it('renders children in the main content area', () => {
    render(
      <Wrapper>
        <PrivateCabinetLayout>
          <div data-testid="child-content">Page content</div>
        </PrivateCabinetLayout>
      </Wrapper>,
    );
    expect(screen.getByTestId('child-content')).toBeInTheDocument();
    expect(screen.getByText('Page content')).toBeInTheDocument();
  });

  it('renders AppHeader with logo', () => {
    render(
      <Wrapper>
        <PrivateCabinetLayout>
          <div>Content</div>
        </PrivateCabinetLayout>
      </Wrapper>,
    );
    expect(screen.getByText('Testurio')).toBeInTheDocument();
  });

  it('renders AppSidebar with navigation links', () => {
    render(
      <Wrapper>
        <PrivateCabinetLayout>
          <div>Content</div>
        </PrivateCabinetLayout>
      </Wrapper>,
    );
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Projects')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders a <main> element containing the children', () => {
    render(
      <Wrapper>
        <PrivateCabinetLayout>
          <div data-testid="page">page</div>
        </PrivateCabinetLayout>
      </Wrapper>,
    );
    const main = screen.getByRole('main');
    expect(main).toContainElement(screen.getByTestId('page'));
  });
});
