import { render, screen, fireEvent } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { SubscriptionStatusResponse } from '@/types/plan.types';

// ─── Mocks ───────────────────────────────────────────────────────────────────

jest.mock('next/link', () => ({
  __esModule: true,
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

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
        details: {
          title: 'Subscription',
          plan: 'Plan',
          billingInterval: 'Billing interval',
          monthly: 'Monthly',
          annual: 'Annual',
          nextBillingDate: 'Next billing date',
          paymentMethod: 'Payment method',
          cardEndingIn: 'Card ending in {{last4}}',
          cardExpiry: 'Expires {{month}}/{{year}}',
          invoices: 'Invoice history',
          invoiceDate: 'Date',
          invoiceAmount: 'Amount',
          invoiceStatus: 'Status',
          invoicePdf: 'PDF',
          invoicePdfLink: 'Download',
          noInvoices: 'No invoices yet.',
          manageBilling: 'Manage billing',
          noPlan: 'No active subscription',
          noPlanCta: 'View plans',
          expiredMessage: 'Your plan has expired.',
          expiredCta: 'Renew plan',
        },
        reactivateDialog: { errorMessage: 'Failed to reactivate subscription. Please try again.' },
      },
    },
  },
});

// ─── Component ────────────────────────────────────────────────────────────────

import SubscriptionDetailsSection from './SubscriptionDetailsSection';

const theme = createTheme();

function renderComponent() {
  return render(
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>
        <SubscriptionDetailsSection />
      </I18nextProvider>
    </ThemeProvider>,
  );
}

const FUTURE = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString();

const BASE: SubscriptionStatusResponse = {
  status: 'Active',
  plan: 'TestPro',
  billingInterval: 'monthly',
  trialEndsAt: null,
  currentPeriodEnd: FUTURE,
  cancelledAt: null,
  paymentMethodLast4: '4242',
  paymentMethodExpMonth: 12,
  paymentMethodExpYear: 2027,
  invoices: [
    {
      date: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
      amount: 29,
      currency: 'usd',
      status: 'paid',
      pdfUrl: 'https://pay.stripe.com/invoice/mock/pdf',
    },
  ],
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SubscriptionDetailsSection', () => {
  beforeEach(() => {
    mockSubscriptionState.data = undefined;
    mockMutate.mockReset();
    (mockPortalSession as { isPending: boolean }).isPending = false;
  });

  it('renders plan name and billing interval', () => {
    mockSubscriptionState.data = BASE;
    renderComponent();
    expect(screen.getByText('Plan')).toBeInTheDocument();
    expect(screen.getByText('TestPro')).toBeInTheDocument();
    expect(screen.getByText('Monthly')).toBeInTheDocument();
  });

  it('renders payment method last 4', () => {
    mockSubscriptionState.data = BASE;
    renderComponent();
    expect(screen.getByText('Card ending in 4242')).toBeInTheDocument();
  });

  it('renders invoice table with PDF link', () => {
    mockSubscriptionState.data = BASE;
    renderComponent();
    expect(screen.getByText('Invoice history')).toBeInTheDocument();
    const pdfLink = screen.getByRole('link', { name: /download/i });
    expect(pdfLink).toHaveAttribute('href', 'https://pay.stripe.com/invoice/mock/pdf');
  });

  it('"Manage billing" button is disabled while portal session is loading', () => {
    mockSubscriptionState.data = BASE;
    (mockPortalSession as { isPending: boolean }).isPending = true;
    renderComponent();
    const btn = screen.getByRole('button', { name: /manage billing/i });
    expect(btn).toBeDisabled();
  });

  it('calls createPortalSession mutate when "Manage billing" is clicked', () => {
    mockSubscriptionState.data = BASE;
    renderComponent();
    fireEvent.click(screen.getByRole('button', { name: /manage billing/i }));
    expect(mockMutate).toHaveBeenCalledTimes(1);
  });

  it('shows "/pricing" CTA when status is None', () => {
    mockSubscriptionState.data = { ...BASE, status: 'None', plan: null, billingInterval: null };
    renderComponent();
    expect(screen.getByText('No active subscription')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /view plans/i });
    expect(link).toHaveAttribute('href', '/pricing');
  });

  it('shows "Expired" message and CTA when status is Expired', () => {
    mockSubscriptionState.data = { ...BASE, status: 'Expired' };
    renderComponent();
    expect(screen.getByText('Your plan has expired.')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /renew plan/i });
    expect(link).toHaveAttribute('href', '/pricing');
  });
});
