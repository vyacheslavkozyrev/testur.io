import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { SubscriptionStatusResponse } from '@/types/plan.types';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockMutate = jest.fn();
const mockSubscriptionState: { data: SubscriptionStatusResponse | undefined } = { data: undefined };
const mockPortalSession = { mutate: mockMutate, isPending: false };

jest.mock('@/hooks/useBilling', () => ({
  useSubscriptionStatus: () => mockSubscriptionState,
  useCreatePortalSession: () => mockPortalSession,
}));

// ─── i18n ────────────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      subscriptionManagement: {
        paymentFailedBanner: {
          message:
            'Your last payment failed. Please update your payment method to avoid losing access.',
          updateCta: 'Update payment method',
        },
        reactivateDialog: { errorMessage: 'Failed to reactivate subscription. Please try again.' },
      },
    },
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

import PaymentFailedBanner from './PaymentFailedBanner';

const theme = createTheme();

function renderBanner() {
  return render(
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>
        <PaymentFailedBanner />
      </I18nextProvider>
    </ThemeProvider>,
  );
}

const BASE: SubscriptionStatusResponse = {
  status: 'PaymentFailed',
  plan: 'TestPro',
  billingInterval: 'monthly',
  trialEndsAt: null,
  currentPeriodEnd: new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString(),
  cancelledAt: null,
  paymentMethodLast4: null,
  paymentMethodExpMonth: null,
  paymentMethodExpYear: null,
  invoices: [],
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PaymentFailedBanner', () => {
  beforeEach(() => {
    mockSubscriptionState.data = undefined;
    mockMutate.mockReset();
    (mockPortalSession as { isPending: boolean }).isPending = false;
  });

  it('renders when status is PaymentFailed', () => {
    mockSubscriptionState.data = BASE;
    renderBanner();
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Your last payment failed. Please update your payment method to avoid losing access.',
      ),
    ).toBeInTheDocument();
  });

  it('calls createPortalSession when "Update payment method" is clicked', () => {
    mockSubscriptionState.data = BASE;
    renderBanner();
    fireEvent.click(screen.getByRole('button', { name: /update payment method/i }));
    expect(mockMutate).toHaveBeenCalledTimes(1);
  });

  it.each(['Active', 'Trialing', 'None', 'Expired', 'CancelledPendingExpiry'] as const)(
    'is not rendered when status is %s',
    (status) => {
      mockSubscriptionState.data = { ...BASE, status };
      const { container } = renderBanner();
      expect(container.firstChild).toBeNull();
    },
  );
});
