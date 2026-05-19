import { render, screen, act, waitFor } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { SubscriptionStatusResponse } from '@/types/plan.types';

// ─── Mock next/navigation ─────────────────────────────────────────────────────

const mockReplace = jest.fn();

jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
  useSearchParams: jest.fn(),
}));

import { useSearchParams } from 'next/navigation';

// ─── Mock useBilling hook ─────────────────────────────────────────────────────

const mockSubscriptionState: { data: SubscriptionStatusResponse | undefined } = {
  data: undefined,
};

jest.mock('@/hooks/useBilling', () => ({
  useSubscriptionStatus: () => mockSubscriptionState,
  useCreateCheckoutSession: () => ({ mutate: jest.fn(), isPending: false }),
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      checkoutSuccess: {
        loading: { message: 'Activating your trial, please wait…' },
        confirmed: {
          title: "Your free trial has started!",
          message: "Your 14-day free trial is now active.",
          cta: "Create your first project",
        },
        timeout: {
          message: "It's taking longer than expected.",
        },
      },
    },
  },
});

// ─── Helpers ──────────────────────────────────────────────────────────────────

const theme = createTheme();

function renderPage() {
  const { default: CheckoutSuccessPage } = jest.requireActual(
    './CheckoutSuccessPage',
  ) as { default: React.ComponentType };

  // Re-import after mocking
  const Page = require('./CheckoutSuccessPage').default;

  return render(
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>
        <Page />
      </I18nextProvider>
    </ThemeProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('CheckoutSuccessPage', () => {
  beforeEach(() => {
    mockReplace.mockClear();
    mockSubscriptionState.data = undefined;
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
    jest.resetModules();
  });

  it('redirects to /pricing when session_id param is absent', () => {
    (useSearchParams as jest.Mock).mockReturnValue({
      get: (_: string) => null,
    });

    renderPage();

    expect(mockReplace).toHaveBeenCalledWith('/pricing');
  });

  it('shows loading state when session_id is present and status is not yet terminal', () => {
    (useSearchParams as jest.Mock).mockReturnValue({
      get: (key: string) => (key === 'session_id' ? 'cs_test_123' : null),
    });
    mockSubscriptionState.data = {
      status: 'None',
      plan: null,
      billingInterval: null,
      trialEndsAt: null,
    };

    renderPage();

    expect(screen.getByText('Activating your trial, please wait…')).toBeInTheDocument();
  });

  it('shows confirmation when subscription status becomes Trialing', async () => {
    (useSearchParams as jest.Mock).mockReturnValue({
      get: (key: string) => (key === 'session_id' ? 'cs_test_123' : null),
    });
    mockSubscriptionState.data = {
      status: 'Trialing',
      plan: 'TestPro',
      billingInterval: 'monthly',
      trialEndsAt: new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString(),
    };

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Your free trial has started!")).toBeInTheDocument();
    });
    expect(screen.getByText('Create your first project')).toBeInTheDocument();
  });

  it('shows support message after 30 s timeout without status resolution', async () => {
    (useSearchParams as jest.Mock).mockReturnValue({
      get: (key: string) => (key === 'session_id' ? 'cs_test_123' : null),
    });
    mockSubscriptionState.data = {
      status: 'None',
      plan: null,
      billingInterval: null,
      trialEndsAt: null,
    };

    renderPage();

    act(() => {
      jest.advanceTimersByTime(30_000);
    });

    await waitFor(() => {
      expect(screen.getByText("It's taking longer than expected.")).toBeInTheDocument();
    });
  });
});
