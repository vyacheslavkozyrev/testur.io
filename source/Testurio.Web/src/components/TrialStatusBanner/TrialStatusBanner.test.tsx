import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { SubscriptionStatusResponse } from '@/types/plan.types';

// ─── Mock next/link ───────────────────────────────────────────────────────────

jest.mock('next/link', () => ({
  __esModule: true,
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// ─── Mock useBilling ─────────────────────────────────────────────────────────

const mockSubscriptionState: { data: SubscriptionStatusResponse | undefined } = {
  data: undefined,
};

jest.mock('@/hooks/useBilling', () => ({
  useSubscriptionStatus: () => mockSubscriptionState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      billing: {
        banner: {
          daysRemaining_one: '{{count}} day remaining in your trial.',
          daysRemaining_other: '{{count}} days remaining in your trial.',
          expired: 'Your trial has expired.',
          upgradeCta: 'Upgrade now',
        },
      },
    },
  },
});

// ─── Component import ─────────────────────────────────────────────────────────

import TrialStatusBanner from './TrialStatusBanner';

// ─── Helpers ──────────────────────────────────────────────────────────────────

const theme = createTheme();

function renderBanner() {
  return render(
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>
        <TrialStatusBanner />
      </I18nextProvider>
    </ThemeProvider>,
  );
}

function trialEndsAt(daysFromNow: number): string {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  return d.toISOString();
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('TrialStatusBanner', () => {
  beforeEach(() => {
    mockSubscriptionState.data = undefined;
  });

  it('renders nothing when subscription data is not yet loaded', () => {
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('is hidden when status is Active', () => {
    mockSubscriptionState.data = {
      status: 'Active',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: null,
    };
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('is hidden when status is None', () => {
    mockSubscriptionState.data = {
      status: 'None',
      plan: null,
      billingInterval: null,
      trialEndsAt: null,
    };
    const { container } = renderBanner();
    expect(container.firstChild).toBeNull();
  });

  it('shows correct day count when more than 3 days remain', () => {
    mockSubscriptionState.data = {
      status: 'Trialing',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: trialEndsAt(10),
    };
    renderBanner();
    expect(screen.getByText(/10 days remaining/i)).toBeInTheDocument();
  });

  it('shows singular day count when exactly 1 day remains', () => {
    mockSubscriptionState.data = {
      status: 'Trialing',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: trialEndsAt(1),
    };
    renderBanner();
    expect(screen.getByText(/1 day remaining/i)).toBeInTheDocument();
  });

  it('renders in amber/warning style when 3 or fewer days remain', () => {
    mockSubscriptionState.data = {
      status: 'Trialing',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: trialEndsAt(2),
    };
    renderBanner();
    // MUI Alert with severity="warning" includes role="alert"
    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(screen.getByText(/2 days remaining/i)).toBeInTheDocument();
  });

  it('renders "Trial expired" variant when status is Expired', () => {
    mockSubscriptionState.data = {
      status: 'Expired',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: null,
    };
    renderBanner();
    expect(screen.getByText('Your trial has expired.')).toBeInTheDocument();
  });

  it('includes "Upgrade now" CTA linking to /pricing', () => {
    mockSubscriptionState.data = {
      status: 'Trialing',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: trialEndsAt(5),
    };
    renderBanner();
    const link = screen.getByRole('link', { name: /upgrade now/i });
    expect(link).toHaveAttribute('href', '/pricing');
  });
});
