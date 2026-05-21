import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import QuotaUsageBar from './QuotaUsageBar';
import type { QuotaUsage } from '@/types/dashboard.types';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      dashboard: {
        quota: {
          usage: '{{used}} / {{limit}} runs used this month',
          usageUnlimited: '{{used}} runs used this month',
          resetsOn: 'Resets on {{date}}',
          noActivePlan: 'No active plan',
          unlimited: 'Unlimited',
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

const RESETS_AT = '2026-06-01T00:00:00Z';

function makeQuota(used: number, limit: number): QuotaUsage {
  return { usedThisMonth: used, monthlyLimit: limit, resetsAt: RESETS_AT };
}

describe('QuotaUsageBar', () => {
  // AC-039: monthly copy renders
  it('shows monthly numeric ratio when under limit', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(3, 50)} />
      </Wrapper>,
    );
    expect(screen.getByText('3 / 50 runs used this month')).toBeInTheDocument();
  });

  it('renders a progress bar when monthlyLimit > 0', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(10, 50)} />
      </Wrapper>,
    );
    const progressBar = container.querySelector('[role="progressbar"]');
    expect(progressBar).toBeInTheDocument();
  });

  // AC-042: amber at >= 80% threshold
  it('renders amber (warning) color when usedThisMonth is at 80% of monthlyLimit', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(40, 50)} />
      </Wrapper>,
    );
    const bar = container.querySelector('[class*="colorWarning"]');
    expect(bar).toBeInTheDocument();
  });

  // AC-041: red at/over limit
  it('renders red (error) color when usedThisMonth equals monthlyLimit', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(50, 50)} />
      </Wrapper>,
    );
    const bar = container.querySelector('[class*="colorError"]');
    expect(bar).toBeInTheDocument();
  });

  it('renders red (error) color when usedThisMonth exceeds monthlyLimit', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(55, 50)} />
      </Wrapper>,
    );
    const bar = container.querySelector('[class*="colorError"]');
    expect(bar).toBeInTheDocument();
  });

  // AC-040: unlimited plan shows usage count without numeric limit, no amber/red, no reset date
  it('renders usage-only label (no numeric limit) when monthlyLimit is -1', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(5, -1)} />
      </Wrapper>,
    );
    expect(screen.getByText('5 runs used this month')).toBeInTheDocument();
    // Must not show a numeric limit fraction
    expect(screen.queryByText(/\/ -1/)).not.toBeInTheDocument();
  });

  it('does not render "Resets on" caption when monthlyLimit is -1', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(5, -1)} />
      </Wrapper>,
    );
    expect(screen.queryByText(/Resets on/)).not.toBeInTheDocument();
  });

  it('does not render amber or red bar when monthlyLimit is -1', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(999, -1)} />
      </Wrapper>,
    );
    expect(container.querySelector('[class*="colorWarning"]')).not.toBeInTheDocument();
    expect(container.querySelector('[class*="colorError"]')).not.toBeInTheDocument();
  });

  // AC-039: "No active plan" when monthlyLimit === 0
  it('shows "No active plan" when monthlyLimit is 0', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(0, 0)} />
      </Wrapper>,
    );
    expect(screen.getByText('No active plan')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('does not render progress bar when monthlyLimit is 0', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(0, 0)} />
      </Wrapper>,
    );
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });
});
