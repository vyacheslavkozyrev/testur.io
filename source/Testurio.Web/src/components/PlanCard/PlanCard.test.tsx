import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { PlanDefinition } from '@/types/plan.types';

// ─── Mock next/link ───────────────────────────────────────────────────────────

jest.mock('next/link', () => {
  const Link = ({ href, children, ...rest }: { href: string; children: React.ReactNode; [key: string]: unknown }) => (
    <a href={href} {...rest}>{children}</a>
  );
  Link.displayName = 'MockLink';
  return Link;
});

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      pricing: {
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

// ─── Lazy import ──────────────────────────────────────────────────────────────

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { default: PlanCard } = require('./PlanCard') as { default: React.ComponentType<import('./PlanCard').PlanCardProps> };

const mockPlan: PlanDefinition = {
  id: 'test-pro',
  name: 'Test Pro',
  monthlyPrice: 49,
  annualPrice: 470,
  annualDiscountPercent: 20,
  isPopular: true,
  features: [
    'Unlimited test runs',
    'API & UI end-to-end testing',
    'AI memory layer',
    'ADO & Jira post-back',
    'Email support',
  ],
};

const freePlan: PlanDefinition = {
  id: 'test-junior',
  name: 'Test Junior',
  monthlyPrice: 0,
  annualPrice: 0,
  annualDiscountPercent: 0,
  isPopular: false,
  features: [
    'Up to 3 projects',
    '50 automated test runs / day',
    'API test execution',
    'Basic test reports',
    'Community support',
  ],
};

describe('PlanCard', () => {
  it('renders the plan name', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.getByText('Test Pro')).toBeInTheDocument();
  });

  it('renders monthly price when interval is monthly', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.getByText('$49')).toBeInTheDocument();
    expect(screen.queryByText('Save 20%')).not.toBeInTheDocument();
  });

  it('renders annual monthly-equivalent price when interval is annual', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="annual" isAuthenticated={false} />
      </Wrapper>,
    );

    // Annual total = 470 ÷ 12 = ~39
    expect(screen.getByText('$39')).toBeInTheDocument();
  });

  it('renders discount badge when interval is annual and discount > 0', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="annual" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.getByText('Save 20%')).toBeInTheDocument();
  });

  it('does not render discount badge on monthly interval', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.queryByText('Save 20%')).not.toBeInTheDocument();
  });

  it('renders Most popular badge for popular plan', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.getByText('Most popular')).toBeInTheDocument();
  });

  it('does not render Most popular badge for non-popular plan', () => {
    render(
      <Wrapper>
        <PlanCard plan={freePlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.queryByText('Most popular')).not.toBeInTheDocument();
  });

  it('renders all feature items', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    for (const feature of mockPlan.features) {
      expect(screen.getByText(feature)).toBeInTheDocument();
    }
  });

  it('CTA href contains plan id and interval for unauthenticated guest', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    const cta = screen.getByRole('link', { name: 'Get started free' });
    expect(cta).toHaveAttribute('href', expect.stringContaining('plan=test-pro'));
    expect(cta).toHaveAttribute('href', expect.stringContaining('interval=monthly'));
  });

  it('renders Upgrade label and billing href for authenticated user', () => {
    render(
      <Wrapper>
        <PlanCard plan={mockPlan} interval="annual" isAuthenticated={true} />
      </Wrapper>,
    );

    const cta = screen.getByRole('link', { name: 'Upgrade' });
    expect(cta).toHaveAttribute('href', expect.stringContaining('/billing'));
    expect(cta).toHaveAttribute('href', expect.stringContaining('plan=test-pro'));
    expect(cta).toHaveAttribute('href', expect.stringContaining('interval=annual'));
  });

  it('renders Free label for zero-price plan', () => {
    render(
      <Wrapper>
        <PlanCard plan={freePlan} interval="monthly" isAuthenticated={false} />
      </Wrapper>,
    );

    expect(screen.getByText('Free')).toBeInTheDocument();
  });
});
