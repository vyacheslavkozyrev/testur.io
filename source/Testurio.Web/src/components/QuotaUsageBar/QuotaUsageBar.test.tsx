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
          usage: '{{used}} / {{limit}} runs used today',
          resetsAt: 'Resets at {{time}}',
          noActivePlan: 'No active plan',
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

const RESETS_AT = '2026-05-17T00:00:00Z';

function makeQuota(used: number, limit: number): QuotaUsage {
  return { usedToday: used, dailyLimit: limit, resetsAt: RESETS_AT };
}

describe('QuotaUsageBar', () => {
  it('shows numeric ratio when under limit', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(3, 50)} />
      </Wrapper>,
    );
    expect(screen.getByText('3 / 50 runs used today')).toBeInTheDocument();
  });

  it('renders a progress bar when dailyLimit > 0', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(10, 50)} />
      </Wrapper>,
    );
    const progressBar = container.querySelector('[role="progressbar"]');
    expect(progressBar).toBeInTheDocument();
  });

  it('renders amber (warning) color when usedToday equals dailyLimit', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(50, 50)} />
      </Wrapper>,
    );
    // MUI LinearProgress renders a div with colorWarning class at limit
    const bar = container.querySelector('[class*="colorWarning"]');
    expect(bar).toBeInTheDocument();
  });

  it('renders red (error) color when usedToday exceeds dailyLimit', () => {
    const { container } = render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(55, 50)} />
      </Wrapper>,
    );
    const bar = container.querySelector('[class*="colorError"]');
    expect(bar).toBeInTheDocument();
  });

  it('shows "No active plan" when dailyLimit is 0', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(0, 0)} />
      </Wrapper>,
    );
    expect(screen.getByText('No active plan')).toBeInTheDocument();
    // No progress bar when no plan
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('does not render progress bar when dailyLimit is 0', () => {
    render(
      <Wrapper>
        <QuotaUsageBar quotaUsage={makeQuota(0, 0)} />
      </Wrapper>,
    );
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });
});
