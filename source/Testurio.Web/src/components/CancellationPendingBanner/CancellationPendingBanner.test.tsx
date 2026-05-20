import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { SubscriptionStatusResponse } from '@/types/plan.types';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockSubscriptionState: { data: SubscriptionStatusResponse | undefined } = { data: undefined };
const mockReactivate = { mutate: jest.fn(), isPending: false };

jest.mock('@/hooks/useBilling', () => ({
  useSubscriptionStatus: () => mockSubscriptionState,
  useReactivateSubscription: () => mockReactivate,
}));

// ReactivateConfirmDialog is tested separately; stub it here.
jest.mock('@/components/ReactivateConfirmDialog/ReactivateConfirmDialog', () => ({
  __esModule: true,
  default: ({ open }: { open: boolean }) =>
    open ? <div data-testid="reactivate-dialog" /> : null,
}));

// ─── i18n ────────────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      subscriptionManagement: {
        cancellationBanner: {
          message: 'Your subscription is cancelled. Access continues until {{date}}.',
          reactivateCta: 'Reactivate',
        },
        reactivateDialog: {
          title: 'Reactivate subscription',
          confirmCta: 'Confirm reactivation',
          cancelCta: 'Cancel',
          errorMessage: 'Failed to reactivate subscription. Please try again.',
          message: 'Your {{plan}} plan will be reactivated. You will be charged {{amount}} on {{date}}.',
        },
      },
    },
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

import CancellationPendingBanner from './CancellationPendingBanner';

const theme = createTheme();

function renderBanner() {
  return render(
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>
        <CancellationPendingBanner />
      </I18nextProvider>
    </ThemeProvider>,
  );
}

const FUTURE = new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString();

const BASE: SubscriptionStatusResponse = {
  status: 'CancelledPendingExpiry',
  plan: 'TestPro',
  billingInterval: 'monthly',
  trialEndsAt: null,
  currentPeriodEnd: FUTURE,
  cancelledAt: new Date().toISOString(),
  paymentMethodLast4: null,
  paymentMethodExpMonth: null,
  paymentMethodExpYear: null,
  invoices: [],
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('CancellationPendingBanner', () => {
  beforeEach(() => {
    mockSubscriptionState.data = undefined;
    mockReactivate.mutate.mockReset();
  });

  it('renders when status is CancelledPendingExpiry', () => {
    mockSubscriptionState.data = BASE;
    renderBanner();
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reactivate/i })).toBeInTheDocument();
  });

  it('shows the correct period end date in the banner text', () => {
    mockSubscriptionState.data = BASE;
    renderBanner();
    const formatted = new Intl.DateTimeFormat(undefined, { dateStyle: 'long' }).format(new Date(FUTURE));
    expect(screen.getByText(new RegExp(formatted))).toBeInTheDocument();
  });

  it('opens ReactivateConfirmDialog when "Reactivate" is clicked', () => {
    mockSubscriptionState.data = BASE;
    renderBanner();
    fireEvent.click(screen.getByRole('button', { name: /reactivate/i }));
    expect(screen.getByTestId('reactivate-dialog')).toBeInTheDocument();
  });

  it.each(['Active', 'Trialing', 'None', 'Expired', 'PaymentFailed'] as const)(
    'is not rendered when status is %s',
    (status) => {
      mockSubscriptionState.data = { ...BASE, status };
      const { container } = renderBanner();
      expect(container.firstChild).toBeNull();
    },
  );
});
