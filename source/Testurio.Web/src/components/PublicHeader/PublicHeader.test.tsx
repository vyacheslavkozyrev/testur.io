import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { AuthUser } from '@/types/layout.types';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  usePathname: jest.fn(() => '/'),
  useRouter: jest.fn(() => ({ push: jest.fn() })),
}));

// ─── Mock next/link ───────────────────────────────────────────────────────────

jest.mock('next/link', () => {
  const Link = ({ href, children, ...rest }: { href: string; children: React.ReactNode; [key: string]: unknown }) => (
    <a href={href} {...rest}>{children}</a>
  );
  Link.displayName = 'MockLink';
  return Link;
});

// ─── Mock useAuthUser ─────────────────────────────────────────────────────────

let mockAuthUser: AuthUser | null = null;

jest.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => mockAuthUser,
}));

// ─── Mock useMediaQuery ───────────────────────────────────────────────────────

let mockIsMobile = false;

jest.mock('@mui/material/useMediaQuery', () => ({
  __esModule: true,
  default: () => mockIsMobile,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      landing: {
        publicHeader: {
          logoAriaLabel: 'Testurio home',
          nav: {
            home: 'Home',
            pricing: 'Pricing',
          },
          action: {
            signIn: 'Sign In',
            getStarted: 'Get Started',
            dashboard: 'Go to Dashboard',
            openMenu: 'Open navigation menu',
            closeMenu: 'Close navigation menu',
          },
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

beforeEach(() => {
  jest.clearAllMocks();
  mockAuthUser = null;
  mockIsMobile = false;
});

// Lazy import after mocks are set up
// eslint-disable-next-line @typescript-eslint/no-require-imports
const { default: PublicHeader } = require('./PublicHeader') as { default: React.ComponentType };

describe('PublicHeader', () => {
  it('renders Sign In and Get Started for unauthenticated visitors', () => {
    mockAuthUser = null;

    render(
      <Wrapper>
        <PublicHeader />
      </Wrapper>,
    );

    expect(screen.getByText('Sign In')).toBeInTheDocument();
    expect(screen.getByText('Get Started')).toBeInTheDocument();
    expect(screen.queryByText('Go to Dashboard')).not.toBeInTheDocument();
  });

  it('renders Go to Dashboard for authenticated users', () => {
    mockAuthUser = {
      id: 'user-1',
      firstName: null,
      lastName: null,
      email: 'user@example.com',
      displayName: 'Test User',
    };

    render(
      <Wrapper>
        <PublicHeader />
      </Wrapper>,
    );

    expect(screen.getByText('Go to Dashboard')).toBeInTheDocument();
    expect(screen.queryByText('Sign In')).not.toBeInTheDocument();
    expect(screen.queryByText('Get Started')).not.toBeInTheDocument();
  });

  it('shows active link styling for the current route', () => {
    const { usePathname } = require('next/navigation') as { usePathname: jest.Mock };
    usePathname.mockReturnValue('/');
    mockAuthUser = null;

    render(
      <Wrapper>
        <PublicHeader />
      </Wrapper>,
    );

    // The Home nav link should have aria-current="page" on desktop
    const homeLink = screen.getByRole('link', { name: 'Home' });
    expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  it('shows hamburger icon on mobile viewports', () => {
    mockIsMobile = true;

    render(
      <Wrapper>
        <PublicHeader />
      </Wrapper>,
    );

    expect(screen.getByRole('button', { name: 'Open navigation menu' })).toBeInTheDocument();
  });

  it('does not show hamburger on desktop viewports', () => {
    mockIsMobile = false;

    render(
      <Wrapper>
        <PublicHeader />
      </Wrapper>,
    );

    expect(screen.queryByRole('button', { name: 'Open navigation menu' })).not.toBeInTheDocument();
  });
});
