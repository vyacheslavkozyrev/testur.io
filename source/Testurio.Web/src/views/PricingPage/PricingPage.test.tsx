import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { PlanDefinition } from '@/types/plan.types';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

jest.mock('next/navigation', () => ({
  usePathname: jest.fn(() => '/pricing'),
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

// ─── Mock useMediaQuery ───────────────────────────────────────────────────────

jest.mock('@mui/material/useMediaQuery', () => ({
  __esModule: true,
  default: () => false,
}));

// ─── Mock useAuthUser ─────────────────────────────────────────────────────────

jest.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => null,
}));

// ─── Mock usePlans ────────────────────────────────────────────────────────────

const mockRefetch = jest.fn();
const mockUsePlansState: {
  data: PlanDefinition[] | undefined;
  isPending: boolean;
  isError: boolean;
  refetch: () => Promise<unknown>;
} = {
  data: undefined,
  isPending: false,
  isError: false,
  refetch: mockRefetch,
};

jest.mock('@/hooks/usePlans', () => ({
  usePlans: () => mockUsePlansState,
}));

jest.mock('@/hooks/useBilling', () => ({
  useSubscriptionStatus: () => ({ data: undefined, isPending: false, isError: false }),
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
          nav: { home: 'Home', pricing: 'Pricing' },
          action: {
            signIn: 'Sign In',
            getStarted: 'Get Started',
            dashboard: 'Go to Dashboard',
            openMenu: 'Open navigation menu',
            closeMenu: 'Close navigation menu',
          },
        },
        publicFooter: {
          logoAriaLabel: 'Testurio home',
          nav: { home: 'Home', pricing: 'Pricing' },
          legal: { privacy: 'Privacy Policy', terms: 'Terms of Service' },
          copyright: '© 2026 Testurio. All rights reserved.',
        },
      },
      pricing: {
        page: {
          title: 'Choose the plan',
          subtitle: 'Start free.',
          error: 'Failed to load plans. Please try again.',
          retry: 'Try again',
        },
        billingToggle: {
          ariaLabel: 'Billing interval',
          monthly: 'Monthly',
          annual: 'Annual',
          saveBadge: 'Save {{percent}}%',
        },
        planCard: {
          free: 'Free',
          perMonth: '/ mo',
          mostPopular: 'Most popular',
          annualDiscount: 'Save {{percent}}%',
          getStarted: 'Get started free',
          upgrade: 'Upgrade',
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

const mockPlans: PlanDefinition[] = [
  {
    id: 'test-junior',
    name: 'Test Junior',
    monthlyPrice: 0,
    annualPrice: 0,
    annualDiscountPercent: 0,
    isPopular: false,
    features: ['Up to 3 projects', '50 test runs / day', 'API testing', 'Basic reports', 'Community support'],
  },
  {
    id: 'test-pro',
    name: 'Test Pro',
    monthlyPrice: 49,
    annualPrice: 470,
    annualDiscountPercent: 20,
    isPopular: true,
    features: ['Unlimited projects', 'Unlimited runs', 'API & UI testing', 'AI memory', 'Email support'],
  },
  {
    id: 'team',
    name: 'Team',
    monthlyPrice: 149,
    annualPrice: 1430,
    annualDiscountPercent: 20,
    isPopular: false,
    features: ['Unlimited projects', 'Unlimited runs', 'API & UI testing', 'Cross-project memory', 'Priority support'],
  },
  {
    id: 'centurio',
    name: 'Centurio',
    monthlyPrice: 399,
    annualPrice: 3830,
    annualDiscountPercent: 20,
    isPopular: false,
    features: ['All projects', 'All runs', 'All test types', 'Global memory', 'SLA guarantee'],
  },
];

beforeEach(() => {
  jest.clearAllMocks();
  mockUsePlansState.data = undefined;
  mockUsePlansState.isPending = false;
  mockUsePlansState.isError = false;
});

// Lazy import after mocks are set up
// eslint-disable-next-line @typescript-eslint/no-require-imports
const { default: PricingPage } = require('./PricingPage') as { default: React.ComponentType };

describe('PricingPage', () => {
  it('shows skeleton placeholders while loading', () => {
    mockUsePlansState.isPending = true;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    const skeletons = document.querySelectorAll('.MuiSkeleton-root');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('does not show plan cards while loading', () => {
    mockUsePlansState.isPending = true;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    expect(screen.queryByText('Test Pro')).not.toBeInTheDocument();
  });

  it('renders four plan cards on success', () => {
    mockUsePlansState.data = mockPlans;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    expect(screen.getByText('Test Junior')).toBeInTheDocument();
    expect(screen.getByText('Test Pro')).toBeInTheDocument();
    expect(screen.getByText('Team')).toBeInTheDocument();
    expect(screen.getByText('Centurio')).toBeInTheDocument();
  });

  it('shows error state with retry button on failure', () => {
    mockUsePlansState.isError = true;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    expect(screen.getByText('Failed to load plans. Please try again.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('calls refetch when retry button is clicked', async () => {
    mockUsePlansState.isError = true;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => {
      expect(mockRefetch).toHaveBeenCalledTimes(1);
    });
  });

  it('toggling to Annual updates all card prices simultaneously', () => {
    mockUsePlansState.data = mockPlans;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    // Monthly prices initially shown
    expect(screen.getByText('$49')).toBeInTheDocument();

    // Toggle to Annual
    fireEvent.click(screen.getByRole('button', { name: 'Annual' }));

    // Annual monthly-equivalent for Test Pro: 470 ÷ 12 = 39
    expect(screen.getByText('$39')).toBeInTheDocument();
    // Monthly price should no longer be shown for Test Pro
    expect(screen.queryByText('$49')).not.toBeInTheDocument();
  });

  it('shows discount badges on all eligible plans when Annual is selected', () => {
    mockUsePlansState.data = mockPlans;

    render(
      <Wrapper>
        <PricingPage />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Annual' }));

    const discountBadges = screen.getAllByText('Save 20%');
    // 3 plans have 20% discount (Test Pro, Team, Centurio)
    expect(discountBadges.length).toBeGreaterThanOrEqual(3);
  });
});
